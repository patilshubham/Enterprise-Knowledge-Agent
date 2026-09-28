using System.Text.Json;
using EnterpriseKnowledgeAgent.Application.AzureOpenAI;
using EnterpriseKnowledgeAgent.Application.Execution;
using EnterpriseKnowledgeAgent.Application.Generation;
using EnterpriseKnowledgeAgent.Application.PromptAssembly;
using EnterpriseKnowledgeAgent.Domain.Execution;
using EnterpriseKnowledgeAgent.Shared.Correlation;
using EnterpriseKnowledgeAgent.Shared.Errors;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Application.Tests.Generation;

/// <summary>TASK-GEN-01: unit tests for <see cref="GenerationService"/>'s requested/effective parameter recording.</summary>
public sealed class GenerationServiceTests
{
    private const string ChatDeployment = "gpt-4o";
    private const string NoTopPDeployment = "o1-mini";
    private const string StructuredOutputDeployment = "gpt-4o-structured";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private sealed class FakeTextGenerationService : ITextGenerationService
    {
        public GenerationParameters? LastParameters { get; private set; }

        public string? LastDeployment { get; private set; }

        public TextGenerationResult Result { get; set; } = new("Generated Markdown.", "stop");

        public Exception? ThrowOnGenerate { get; set; }

        public Task<TextGenerationResult> GenerateAsync(IReadOnlyList<ChatMessage> messages, string deployment, GenerationParameters? parameters = null, CancellationToken cancellationToken = default)
        {
            if (ThrowOnGenerate is not null)
            {
                throw ThrowOnGenerate;
            }

            LastDeployment = deployment;
            LastParameters = parameters;
            return Task.FromResult(Result);
        }

        public async IAsyncEnumerable<string> GenerateStreamingAsync(IReadOnlyList<ChatMessage> messages, string deployment, GenerationParameters? parameters = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (ThrowOnGenerate is not null)
            {
                throw ThrowOnGenerate;
            }

            LastDeployment = deployment;
            LastParameters = parameters;
            await Task.CompletedTask;
            yield return Result.Content;
        }

        public Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class FakeDeploymentRegistry : IDeploymentRegistry
    {
        public IReadOnlyList<DeploymentInfo> ListDeployments() =>
        [
            new DeploymentInfo(ChatDeployment, DeploymentPurpose.Chat, 128_000, 4_096, EmbeddingDimensions: null, SupportsTopP: true),
            new DeploymentInfo(NoTopPDeployment, DeploymentPurpose.Chat, 128_000, 65_536, EmbeddingDimensions: null, SupportsTopP: false),
            new DeploymentInfo(StructuredOutputDeployment, DeploymentPurpose.Chat, 128_000, 4_096, EmbeddingDimensions: null, SupportsTopP: true, SupportsStructuredOutput: true),
        ];
    }

    private static AgentRun CreateRun(string deployment, AgentRunParametersSnapshot requested)
    {
        var run = AgentRun.Create(
            applicationId: Guid.NewGuid(),
            agentId: Guid.NewGuid(),
            requestedBy: "keycloak-sub-1",
            userRequest: "Summarize the Q3 report",
            idempotencyKey: Guid.NewGuid().ToString(),
            outputTitle: null,
            agentNameSnapshot: "Support Agent",
            agentPromptSnapshot: "You are a helpful assistant.",
            knowledgeScopeSnapshotJson: null,
            deploymentSnapshot: deployment,
            requestedParametersJson: JsonSerializer.Serialize(requested, JsonOptions),
            effectiveParametersJson: JsonSerializer.Serialize(requested, JsonOptions),
            retrievalSettingsJson: null,
            correlationId: CorrelationId.New(),
            now: Now);
        run.Claim("worker-1", Now, TimeSpan.FromMinutes(5));
        return run;
    }

    private static AssembledPrompt CreatePrompt() =>
        PromptAssembler.Assemble(new PromptAssemblyRequest("Agent prompt.", "User request.", []));

    [Fact]
    public async Task GenerateAsync_for_a_topp_supporting_deployment_sends_and_records_requested_params_unchanged()
    {
        var requested = new AgentRunParametersSnapshot(0.7, 2048, 0.9);
        var run = CreateRun(ChatDeployment, requested);
        var textGeneration = new FakeTextGenerationService();
        var service = new GenerationService(textGeneration, new FakeDeploymentRegistry());

        var outcome = await service.GenerateAsync(run, CreatePrompt());

        textGeneration.LastParameters.Should().Be(new GenerationParameters(0.7, 2048, 0.9));
        outcome.EffectiveParameters.Should().Be(requested);
        run.EffectiveParametersJson.Should().Be(JsonSerializer.Serialize(requested, JsonOptions));
    }

    [Fact]
    public async Task GenerateAsync_for_a_deployment_without_topp_support_drops_topp_from_effective_params()
    {
        var requested = new AgentRunParametersSnapshot(0.7, 2048, 0.9);
        var run = CreateRun(NoTopPDeployment, requested);
        var textGeneration = new FakeTextGenerationService();
        var service = new GenerationService(textGeneration, new FakeDeploymentRegistry());

        var outcome = await service.GenerateAsync(run, CreatePrompt());

        textGeneration.LastParameters.Should().Be(new GenerationParameters(0.7, 2048, null));
        outcome.EffectiveParameters.TopP.Should().BeNull();
        var storedEffective = JsonSerializer.Deserialize<AgentRunParametersSnapshot>(run.EffectiveParametersJson!, JsonOptions);
        storedEffective!.TopP.Should().BeNull();
    }

    [Fact]
    public async Task GenerateAsync_returns_the_generated_content_and_finish_reason()
    {
        var run = CreateRun(ChatDeployment, new AgentRunParametersSnapshot(0.7, 2048, null));
        var textGeneration = new FakeTextGenerationService { Result = new TextGenerationResult("# Answer", "stop") };
        var service = new GenerationService(textGeneration, new FakeDeploymentRegistry());

        var outcome = await service.GenerateAsync(run, CreatePrompt());

        outcome.Content.Should().Be("# Answer");
        outcome.FinishReason.Should().Be("stop");
    }

    [Fact]
    public async Task GenerateAsync_wraps_AzureOpenAiException_as_AgentRunExecutionException_with_the_same_category()
    {
        var run = CreateRun(ChatDeployment, new AgentRunParametersSnapshot(0.7, 2048, null));
        var textGeneration = new FakeTextGenerationService { ThrowOnGenerate = new AzureOpenAiException(ErrorCategory.Throttled, "Rate limited.") };
        var service = new GenerationService(textGeneration, new FakeDeploymentRegistry());

        var act = () => service.GenerateAsync(run, CreatePrompt());

        (await act.Should().ThrowAsync<AgentRunExecutionException>())
            .Which.Category.Should().Be(ErrorCategory.Throttled);
    }

    [Fact]
    public async Task GenerateAsync_for_an_unconfigured_deployment_throws_configuration_error()
    {
        var run = CreateRun("not-configured", new AgentRunParametersSnapshot(0.7, 2048, null));
        var textGeneration = new FakeTextGenerationService();
        var service = new GenerationService(textGeneration, new FakeDeploymentRegistry());

        var act = () => service.GenerateAsync(run, CreatePrompt());

        (await act.Should().ThrowAsync<AgentRunExecutionException>())
            .Which.Category.Should().Be(ErrorCategory.Configuration);
    }

    [Fact]
    public async Task GenerateAsync_for_a_structured_output_capable_deployment_requests_structured_output()
    {
        var run = CreateRun(StructuredOutputDeployment, new AgentRunParametersSnapshot(0.7, 2048, 0.9));
        var textGeneration = new FakeTextGenerationService();
        var service = new GenerationService(textGeneration, new FakeDeploymentRegistry());

        await service.GenerateAsync(run, CreatePrompt());

        textGeneration.LastParameters!.RequestStructuredOutput.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateAsync_for_a_non_structured_deployment_does_not_request_structured_output()
    {
        var run = CreateRun(ChatDeployment, new AgentRunParametersSnapshot(0.7, 2048, 0.9));
        var textGeneration = new FakeTextGenerationService();
        var service = new GenerationService(textGeneration, new FakeDeploymentRegistry());

        await service.GenerateAsync(run, CreatePrompt());

        textGeneration.LastParameters!.RequestStructuredOutput.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateAsync_extracts_markdown_from_a_valid_structured_output_response()
    {
        var run = CreateRun(StructuredOutputDeployment, new AgentRunParametersSnapshot(0.7, 2048, null));
        var textGeneration = new FakeTextGenerationService
        {
            Result = new TextGenerationResult("""{"markdown":"# Answer\n\nSee [SOURCE-001]."}""", "stop"),
        };
        var service = new GenerationService(textGeneration, new FakeDeploymentRegistry());

        var outcome = await service.GenerateAsync(run, CreatePrompt());

        outcome.Content.Should().Be("# Answer\n\nSee [SOURCE-001].");
    }

    [Fact]
    public async Task GenerateAsync_falls_back_to_raw_content_when_the_structured_output_response_is_malformed_json()
    {
        var run = CreateRun(StructuredOutputDeployment, new AgentRunParametersSnapshot(0.7, 2048, null));
        var textGeneration = new FakeTextGenerationService
        {
            Result = new TextGenerationResult("not valid json at all", "stop"),
        };
        var service = new GenerationService(textGeneration, new FakeDeploymentRegistry());

        var outcome = await service.GenerateAsync(run, CreatePrompt());

        outcome.Content.Should().Be("not valid json at all");
    }

    [Fact]
    public async Task GenerateAsync_falls_back_to_raw_content_when_the_structured_output_response_is_missing_markdown()
    {
        var run = CreateRun(StructuredOutputDeployment, new AgentRunParametersSnapshot(0.7, 2048, null));
        var textGeneration = new FakeTextGenerationService
        {
            Result = new TextGenerationResult("""{"somethingElse":"value"}""", "stop"),
        };
        var service = new GenerationService(textGeneration, new FakeDeploymentRegistry());

        var outcome = await service.GenerateAsync(run, CreatePrompt());

        outcome.Content.Should().Be("""{"somethingElse":"value"}""");
    }
}
