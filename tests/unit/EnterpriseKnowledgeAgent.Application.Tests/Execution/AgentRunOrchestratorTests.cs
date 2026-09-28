using System.Text.Json;
using EnterpriseKnowledgeAgent.Application.AzureOpenAI;
using EnterpriseKnowledgeAgent.Application.Citations;
using EnterpriseKnowledgeAgent.Application.Execution;
using EnterpriseKnowledgeAgent.Application.Generation;
using EnterpriseKnowledgeAgent.Application.PromptAssembly;
using EnterpriseKnowledgeAgent.Application.Realtime;
using EnterpriseKnowledgeAgent.Application.Retrieval;
using EnterpriseKnowledgeAgent.Domain.Execution;
using EnterpriseKnowledgeAgent.Domain.Outputs;
using EnterpriseKnowledgeAgent.Shared.Correlation;
using EnterpriseKnowledgeAgent.Shared.Errors;
using EnterpriseKnowledgeAgent.Shared.Execution;
using EnterpriseKnowledgeAgent.Shared.Jobs;
using EnterpriseKnowledgeAgent.Shared.Messaging;
using EnterpriseKnowledgeAgent.Shared.Realtime;
using EnterpriseKnowledgeAgent.Shared.Retrieval;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace EnterpriseKnowledgeAgent.Application.Tests.Execution;

/// <summary>TASK-RUN-02: unit tests for the agent-run pipeline orchestrator, using in-memory fakes for every dependency.</summary>
public sealed class AgentRunOrchestratorTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string WorkerId = "agent-worker-1";

    private sealed class Harness
    {
        public FakeAgentRunRepositoryForOrchestrator RunRepository { get; } = new();
        public FakeOutboxRepositoryForOrchestrator OutboxRepository { get; } = new();
        public FakeKnowledgeFileRepositoryForOrchestrator FileRepository { get; } = new();
        public FakeRunEvidenceRepositoryForOrchestrator RunEvidenceRepository { get; } = new();
        public FakeCitationRepositoryForOrchestrator CitationRepository { get; } = new();
        public FakeGeneratedOutputRepository GeneratedOutputRepository { get; } = new();
        public FakeRetrievalServiceForOrchestrator RetrievalService { get; } = new();
        public FakeTextGenerationServiceForOrchestrator TextGenerationService { get; } = new();
        public FakeDeploymentRegistryForOrchestrator DeploymentRegistry { get; } = new();
        public FakeTokenEstimatorForOrchestrator TokenEstimator { get; } = new();

        /// <summary>TASK-SIG-03: this suite exercises pipeline/persistence logic only - realtime publishing is covered separately.</summary>
        private sealed class NoOpRealtimeEventPublisher : IRealtimeEventPublisher
        {
            public Task PublishAsync<TPayload>(string eventType, IReadOnlyCollection<string> groups, SignalREvent<TPayload> envelope, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
        }

        public AgentRunOrchestrator BuildOrchestrator() => new(
            RunRepository,
            OutboxRepository,
            new ContextBudgetCalculator(DeploymentRegistry, TokenEstimator),
            RetrievalService,
            new RunEvidenceService(RunEvidenceRepository, FileRepository),
            new GenerationService(TextGenerationService, DeploymentRegistry),
            new CitationService(CitationRepository),
            GeneratedOutputRepository,
            new NoOpRealtimeEventPublisher(),
            TimeProvider.System,
            NullLogger<AgentRunOrchestrator>.Instance);

        public (AgentRun Run, AgentExecutionMessage Message) SeedClaimedRun(
            bool strictGrounding = false, bool citationsRequired = false)
        {
            var applicationId = Guid.NewGuid();
            var agentId = Guid.NewGuid();

            var requestedParameters = new AgentRunParametersSnapshot(0.2, 800, null);
            var retrievalSettings = new
            {
                RetrievalMode = nameof(RetrievalMode.Hybrid),
                RetrievalCount = 5,
                MinimumSimilarity = 0.5,
                MaximumEvidenceTokens = 4_000,
                IncludeNeighbors = false,
                StrictGrounding = strictGrounding,
                CitationsRequired = citationsRequired,
            };

            var run = AgentRun.Create(
                applicationId,
                agentId,
                requestedBy: "keycloak-sub-1",
                userRequest: "Summarize the Q3 report",
                idempotencyKey: Guid.NewGuid().ToString(),
                outputTitle: null,
                agentNameSnapshot: "Support Agent",
                agentPromptSnapshot: "You are a helpful assistant.",
                knowledgeScopeSnapshotJson: null,
                deploymentSnapshot: FakeDeploymentRegistryForOrchestrator.ChatDeployment,
                requestedParametersJson: JsonSerializer.Serialize(requestedParameters, JsonOptions),
                effectiveParametersJson: JsonSerializer.Serialize(requestedParameters, JsonOptions),
                retrievalSettingsJson: JsonSerializer.Serialize(retrievalSettings, JsonOptions),
                correlationId: CorrelationId.New(),
                now: Now);
            run.Claim(WorkerId, Now, TimeSpan.FromMinutes(5));
            RunRepository.Runs[run.Id] = run;

            var message = new AgentExecutionMessage(Guid.NewGuid(), applicationId, agentId, run.Id, CorrelationId.New(), run.AttemptNumber);
            return (run, message);
        }

        public static EvidenceSource CreateSource(Guid fileId, string sourceId = "SOURCE-001") =>
            new(sourceId, fileId, Guid.NewGuid(), "notes.txt", "Section 1", "Relevant passage text.", 0, 20, 5, 0.9);
    }

    [Fact]
    public async Task ProcessAsync_happy_path_completes_the_run_and_persists_a_generated_output()
    {
        var harness = new Harness();
        var (run, message) = harness.SeedClaimedRun(citationsRequired: true);
        var fileId = Guid.NewGuid();
        var source = Harness.CreateSource(fileId);
        harness.RetrievalService.Result = new RetrievalResult(NoEvidence: false, Sources: [source], ExcludedFileIds: []);
        harness.TextGenerationService.Result = new("Answer text. [SOURCE-001]", "stop");

        await harness.BuildOrchestrator().ProcessAsync(message, WorkerId);

        run.Status.Should().Be(JobStatus.Completed);
        run.Stage.Should().Be(RunStage.Completed);
        var output = harness.GeneratedOutputRepository.Outputs.Values.Should().ContainSingle().Subject;
        output.AgentRunId.Should().Be(run.Id);
        output.OriginalMarkdown.Should().Be("Answer text. [SOURCE-001]");
        harness.CitationRepository.Citations.Should().ContainSingle();
        harness.RunEvidenceRepository.Evidence.Should().ContainSingle();
    }

    [Fact]
    public async Task ProcessAsync_is_a_no_op_for_a_redelivered_message_after_the_run_already_completed()
    {
        var harness = new Harness();
        var (run, message) = harness.SeedClaimedRun();
        run.Complete(Now);

        var act = async () => await harness.BuildOrchestrator().ProcessAsync(message, WorkerId);

        await act.Should().NotThrowAsync();
        harness.RetrievalService.LastRequest.Should().BeNull();
        harness.GeneratedOutputRepository.Outputs.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_returns_without_error_when_the_run_no_longer_exists()
    {
        var harness = new Harness();
        var message = new AgentExecutionMessage(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CorrelationId.New(), 1);

        var act = async () => await harness.BuildOrchestrator().ProcessAsync(message, WorkerId);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ProcessAsync_strict_grounding_with_no_evidence_is_a_terminal_retrieval_empty_failure()
    {
        var harness = new Harness();
        var (run, message) = harness.SeedClaimedRun(strictGrounding: true);
        harness.RetrievalService.Result = new RetrievalResult(NoEvidence: true, Sources: [], ExcludedFileIds: []);

        var act = async () => await harness.BuildOrchestrator().ProcessAsync(message, WorkerId);

        await act.Should().NotThrowAsync();
        run.Status.Should().Be(JobStatus.Failed);
        run.FailureCategory.Should().Be(ErrorCategory.RetrievalEmpty);
        harness.OutboxRepository.Messages.Should().BeEmpty(); // RetrievalEmpty is not retryable.
    }

    [Fact]
    public async Task ProcessAsync_citations_required_with_no_valid_markers_is_a_terminal_output_validation_failure()
    {
        var harness = new Harness();
        var (run, message) = harness.SeedClaimedRun(citationsRequired: true);
        var source = Harness.CreateSource(Guid.NewGuid());
        harness.RetrievalService.Result = new RetrievalResult(NoEvidence: false, Sources: [source], ExcludedFileIds: []);
        harness.TextGenerationService.Result = new("Answer text with no citation markers.", "stop");

        await harness.BuildOrchestrator().ProcessAsync(message, WorkerId);

        run.Status.Should().Be(JobStatus.Failed);
        run.FailureCategory.Should().Be(ErrorCategory.OutputValidationFailure);
        harness.GeneratedOutputRepository.Outputs.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_retryable_failure_requeues_the_run_via_a_fresh_outbox_message_without_throwing()
    {
        var harness = new Harness();
        var (run, message) = harness.SeedClaimedRun();
        harness.RetrievalService.ThrowOnRetrieve = new TimeoutException("transient AOAI timeout");

        var act = async () => await harness.BuildOrchestrator().ProcessAsync(message, WorkerId);

        await act.Should().NotThrowAsync();
        run.Status.Should().Be(JobStatus.Queued); // RequeueForRetry resets back to Queued for re-delivery.
        run.FailureCategory.Should().BeNull(); // reset by RequeueForRetry
        harness.OutboxRepository.Messages.Should().ContainSingle();
        run.OutboxMessageId.Should().Be(harness.OutboxRepository.Messages.Single().Id);
        run.AttemptNumber.Should().Be(1); // Claim() set it once; RequeueForRetry doesn't touch it - only a future Claim() (on redelivery) increments it again.
        var retryMessage = JsonSerializer.Deserialize<AgentExecutionMessage>(harness.OutboxRepository.Messages.Single().PayloadJson, JsonOptions);
        retryMessage!.AttemptNumber.Should().Be(2); // The queued retry message requests the next attempt.
    }

    [Fact]
    public async Task ProcessAsync_terminal_failure_marks_the_run_failed_without_throwing()
    {
        var harness = new Harness();
        var (run, message) = harness.SeedClaimedRun();
        harness.RetrievalService.ThrowOnRetrieve = new InvalidOperationException("boom - not retryable");

        var act = async () => await harness.BuildOrchestrator().ProcessAsync(message, WorkerId);

        await act.Should().NotThrowAsync();
        run.Status.Should().Be(JobStatus.Failed);
        run.FailureCategory.Should().Be(ErrorCategory.InternalFailure);
        harness.OutboxRepository.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_generation_throttling_is_classified_and_retried_via_the_shared_failure_pipeline()
    {
        // TASK-GEN-03: proves end-to-end (not just GenerationServiceTests' isolated rewrap check)
        // that an AOAI throttling failure during Generating reaches the run as a retryable,
        // correctly-categorized failure via GenerationService's AzureOpenAiException rewrap +
        // AgentRunOrchestrator's shared ClassifyException/RetryClassifier pipeline.
        var harness = new Harness();
        var (run, message) = harness.SeedClaimedRun();
        var source = Harness.CreateSource(Guid.NewGuid());
        harness.RetrievalService.Result = new RetrievalResult(NoEvidence: false, Sources: [source], ExcludedFileIds: []);
        harness.TextGenerationService.ThrowOnGenerate = new AzureOpenAiException(ErrorCategory.Throttled, "Rate limited.");

        var act = async () => await harness.BuildOrchestrator().ProcessAsync(message, WorkerId);

        await act.Should().NotThrowAsync();
        run.Status.Should().Be(JobStatus.Queued); // Throttled is retryable -> requeued, not terminal.
        harness.OutboxRepository.Messages.Should().ContainSingle();
    }

    [Fact]
    public async Task ProcessAsync_honors_a_cancellation_requested_before_the_generating_checkpoint()
    {
        // TASK-RUN-05: a cancel request made while the run was Preparing/Retrieving/
        // BuildingContext only set the CancellationRequested flag (see AgentRunTests);
        // the orchestrator must check it at the last pre-generation checkpoint and
        // transition to Cancelled itself, without ever calling text generation.
        var harness = new Harness();
        var (run, message) = harness.SeedClaimedRun();
        run.RequestCancellation(Now);
        var source = Harness.CreateSource(Guid.NewGuid());
        harness.RetrievalService.Result = new RetrievalResult(NoEvidence: false, Sources: [source], ExcludedFileIds: []);

        var act = async () => await harness.BuildOrchestrator().ProcessAsync(message, WorkerId);

        await act.Should().NotThrowAsync();
        run.Status.Should().Be(JobStatus.Cancelled);
        run.Stage.Should().Be(RunStage.Cancelled);
        harness.TextGenerationService.CallCount.Should().Be(0); // GenerateAsync was never called.
        harness.OutboxRepository.Messages.Should().BeEmpty();
        harness.GeneratedOutputRepository.Outputs.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_resuming_after_a_crash_between_saving_and_completing_does_not_duplicate_the_generated_output()
    {
        var harness = new Harness();
        var (run, message) = harness.SeedClaimedRun(citationsRequired: true);
        var fileId = Guid.NewGuid();
        var source = Harness.CreateSource(fileId);
        harness.RetrievalService.Result = new RetrievalResult(NoEvidence: false, Sources: [source], ExcludedFileIds: []);
        harness.TextGenerationService.Result = new("Answer text. [SOURCE-001]", "stop");

        // Simulate a prior attempt that persisted the output (and its evidence/citations) but crashed
        // before reaching run.Complete() - a redelivered message resumes from Running, not Completed.
        var preExisting = GeneratedOutput.Create(run.ApplicationId, run.Id, "Existing title", "Answer text. [SOURCE-001]", run.RequestedBy, Now);
        harness.GeneratedOutputRepository.Outputs[preExisting.Id] = preExisting;

        await harness.BuildOrchestrator().ProcessAsync(message, WorkerId);

        run.Status.Should().Be(JobStatus.Completed);
        harness.GeneratedOutputRepository.Outputs.Values.Should().ContainSingle().Which.Id.Should().Be(preExisting.Id);
    }
}
