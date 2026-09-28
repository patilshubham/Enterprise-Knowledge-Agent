using EnterpriseKnowledgeAgent.Application.AzureOpenAI;
using EnterpriseKnowledgeAgent.Application.Execution;
using EnterpriseKnowledgeAgent.Application.PromptAssembly;
using EnterpriseKnowledgeAgent.Application.Tokenization;
using EnterpriseKnowledgeAgent.Shared.Errors;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Application.Tests.PromptAssembly;

/// <summary>TASK-PRMT-01: unit tests for <see cref="ContextBudgetCalculator"/>'s budget math + overflow rejection.</summary>
public sealed class ContextBudgetCalculatorTests
{
    private const string ChatDeployment = "gpt-4o";

    /// <summary>One token per character, so tests can reason about exact numbers.</summary>
    private sealed class FakeTokenEstimator : ITokenEstimator
    {
        public int EstimateTokenCount(string text, string? deployment = null) => text.Length;
    }

    private sealed class FakeDeploymentRegistry(int? maxContextTokens) : IDeploymentRegistry
    {
        public IReadOnlyList<DeploymentInfo> ListDeployments() =>
        [
            new DeploymentInfo(ChatDeployment, DeploymentPurpose.Chat, maxContextTokens, 4_096, EmbeddingDimensions: null, SupportsTopP: true),
        ];
    }

    private static ContextBudgetCalculator CreateCalculator(int? maxContextTokens = 1_000) =>
        new(new FakeDeploymentRegistry(maxContextTokens), new FakeTokenEstimator());

    private static ContextBudgetRequest CreateRequest(
        string platformInstructions = "PLATFORM",
        string agentPrompt = "AGENT",
        string userRequest = "USER",
        string protocolInstructions = "PROTOCOL",
        int maximumOutputTokens = 100,
        int maximumEvidenceTokens = 10_000,
        string deployment = ChatDeployment) =>
        new(platformInstructions, agentPrompt, userRequest, protocolInstructions, maximumOutputTokens, maximumEvidenceTokens, deployment);

    [Fact]
    public void Calculate_produces_evidence_budget_from_context_minus_fixed_layers()
    {
        var calculator = CreateCalculator(maxContextTokens: 1_000);
        // Fixed layers: 8 + 5 + 4 + 8 = 25 chars/tokens, plus 100 output reservation = 125.
        var request = CreateRequest();

        var result = calculator.Calculate(request);

        result.FixedLayerTokens.Should().Be(125);
        result.DeploymentContextTokens.Should().Be(1_000);
        result.EvidenceTokenBudget.Should().Be(1_000 - 125);
    }

    [Fact]
    public void Calculate_caps_evidence_budget_at_the_agents_maximum_evidence_tokens()
    {
        var calculator = CreateCalculator(maxContextTokens: 1_000_000);
        var request = CreateRequest(maximumEvidenceTokens: 500);

        var result = calculator.Calculate(request);

        result.EvidenceTokenBudget.Should().Be(500);
    }

    [Fact]
    public void Calculate_when_fixed_layers_exceed_context_throws_ContextOverflow()
    {
        var calculator = CreateCalculator(maxContextTokens: 100);
        var request = CreateRequest(maximumOutputTokens: 1_000);

        var act = () => calculator.Calculate(request);

        act.Should().Throw<AgentRunExecutionException>()
            .Which.Category.Should().Be(ErrorCategory.ContextOverflow);
    }

    [Fact]
    public void Calculate_for_unknown_deployment_throws_configuration_error()
    {
        var calculator = CreateCalculator();
        var request = CreateRequest(deployment: "not-configured");

        var act = () => calculator.Calculate(request);

        act.Should().Throw<AgentRunExecutionException>()
            .Which.Category.Should().Be(ErrorCategory.Configuration);
    }

    [Fact]
    public void Calculate_for_deployment_without_a_context_limit_throws_configuration_error()
    {
        var calculator = CreateCalculator(maxContextTokens: null);
        var request = CreateRequest();

        var act = () => calculator.Calculate(request);

        act.Should().Throw<AgentRunExecutionException>()
            .Which.Category.Should().Be(ErrorCategory.Configuration);
    }
}
