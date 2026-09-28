using EnterpriseKnowledgeAgent.Application.Agents;
using EnterpriseKnowledgeAgent.Application.AzureOpenAI;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Application.Tests.Agents;

/// <summary>TASK-AGT-03: unit tests for <see cref="AgentParameterValidator"/>'s deployment-capability truth table.</summary>
public sealed class AgentParameterValidatorTests
{
    private const string ChatDeployment = "gpt-4o";
    private const string NoTopPDeployment = "o1-mini";
    private const string EmbeddingDeployment = "text-embedding-3-small";

    private sealed class FakeDeploymentRegistry : IDeploymentRegistry
    {
        public IReadOnlyList<DeploymentInfo> ListDeployments() =>
        [
            new DeploymentInfo(ChatDeployment, DeploymentPurpose.Chat, 128_000, 4_096, EmbeddingDimensions: null, SupportsTopP: true),
            new DeploymentInfo(NoTopPDeployment, DeploymentPurpose.Chat, 128_000, 65_536, EmbeddingDimensions: null, SupportsTopP: false),
            new DeploymentInfo(EmbeddingDeployment, DeploymentPurpose.Embedding, null, null, 1536),
        ];
    }

    private static AgentParameterValidator CreateValidator() => new(new FakeDeploymentRegistry());

    [Fact]
    public void ValidateGenerationParameters_accepts_valid_chat_deployment()
    {
        var validator = CreateValidator();

        var act = () => validator.ValidateGenerationParameters(ChatDeployment, 2048, 0.9);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateGenerationParameters_unknown_deployment_throws()
    {
        var validator = CreateValidator();

        var act = () => validator.ValidateGenerationParameters("not-configured", 2048, null);

        act.Should().Throw<AgentValidationException>();
    }

    [Fact]
    public void ValidateGenerationParameters_embedding_deployment_is_rejected()
    {
        var validator = CreateValidator();

        var act = () => validator.ValidateGenerationParameters(EmbeddingDeployment, 2048, null);

        act.Should().Throw<AgentValidationException>();
    }

    [Fact]
    public void ValidateGenerationParameters_maximum_output_tokens_over_limit_throws()
    {
        var validator = CreateValidator();

        var act = () => validator.ValidateGenerationParameters(ChatDeployment, 5000, null);

        act.Should().Throw<AgentValidationException>();
    }

    [Fact]
    public void ValidateGenerationParameters_topP_on_unsupported_deployment_throws()
    {
        var validator = CreateValidator();

        var act = () => validator.ValidateGenerationParameters(NoTopPDeployment, 2048, 0.9);

        act.Should().Throw<AgentValidationException>();
    }

    [Fact]
    public void ValidateGenerationParameters_null_topP_on_unsupported_deployment_is_allowed()
    {
        var validator = CreateValidator();

        var act = () => validator.ValidateGenerationParameters(NoTopPDeployment, 2048, null);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateGenerationParameters_deployment_name_comparison_is_case_insensitive()
    {
        var validator = CreateValidator();

        var act = () => validator.ValidateGenerationParameters(ChatDeployment.ToUpperInvariant(), 2048, null);

        act.Should().NotThrow();
    }
}
