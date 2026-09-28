using System.Diagnostics;
using System.Text.Json;
using EnterpriseKnowledgeAgent.Application.Citations;
using EnterpriseKnowledgeAgent.Application.Generation;
using EnterpriseKnowledgeAgent.Application.Jobs;
using EnterpriseKnowledgeAgent.Application.Outputs;
using EnterpriseKnowledgeAgent.Application.PromptAssembly;
using EnterpriseKnowledgeAgent.Application.Realtime;
using EnterpriseKnowledgeAgent.Application.Retrieval;
using EnterpriseKnowledgeAgent.Domain.Execution;
using EnterpriseKnowledgeAgent.Domain.Operations;
using EnterpriseKnowledgeAgent.Domain.Outputs;
using EnterpriseKnowledgeAgent.Shared.Errors;
using EnterpriseKnowledgeAgent.Shared.Execution;
using EnterpriseKnowledgeAgent.Shared.Jobs;
using EnterpriseKnowledgeAgent.Shared.Messaging;
using EnterpriseKnowledgeAgent.Shared.Observability;
using EnterpriseKnowledgeAgent.Shared.Realtime;
using EnterpriseKnowledgeAgent.Shared.Retrieval;
using Microsoft.Extensions.Logging;

namespace EnterpriseKnowledgeAgent.Application.Execution;

/// <summary>
/// TASK-RUN-02: the agent-run pipeline body (ASDD §16, §17.2/17.3), hosted
/// by the agent worker (module 19). Mirrors
/// <see cref="EnterpriseKnowledgeAgent.Application.Processing.DocumentProcessingOrchestrator"/>'s
/// shape: claim already happened (module 18's <c>IdempotentQueueConsumer</c>
/// calls <see cref="IAgentRunRepository.TryClaimAsync"/> before this type is
/// invoked), so <see cref="ProcessAsync"/> re-fetches the freshly-claimed run
/// and advances it through <see cref="RunStage.Preparing"/> →
/// <see cref="RunStage.Retrieving"/> → <see cref="RunStage.BuildingContext"/> →
/// <see cref="RunStage.Generating"/> → <see cref="RunStage.Validating"/> →
/// <see cref="RunStage.Saving"/> → <see cref="RunStage.Completed"/>,
/// persisting progress before each stage so a crash/restart resumes from the
/// last saved stage rather than losing history. Every outcome (success,
/// requeue, terminal failure) completes without throwing, so the caller
/// always deletes the current SQS message - future attempts are driven
/// entirely by the outbox, never by native SQS redelivery (Decision D1).
/// </summary>
public sealed partial class AgentRunOrchestrator(
    IAgentRunRepository agentRunRepository,
    IOutboxRepository outboxRepository,
    ContextBudgetCalculator contextBudgetCalculator,
    IRetrievalService retrievalService,
    RunEvidenceService runEvidenceService,
    GenerationService generationService,
    CitationService citationService,
    IGeneratedOutputRepository generatedOutputRepository,
    IRealtimeEventPublisher realtimePublisher,
    TimeProvider timeProvider,
    ILogger<AgentRunOrchestrator> logger)
{
    private const double DefaultSemanticWeight = 0.6;
    private const double DefaultKeywordWeight = 0.4;
    private const int MaxTitleLength = 80;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task ProcessAsync(AgentExecutionMessage message, string workerId, CancellationToken cancellationToken = default)
    {
        var run = await agentRunRepository.GetAsync(message.RunId, cancellationToken);
        if (run is null)
        {
            LogRunNotFound(message.RunId, message.MessageId);
            return;
        }

        // Idempotent no-op for a redelivered message that arrives after the
        // run already reached a terminal state (e.g. the SQS delete for a
        // prior successful attempt was lost after processing completed).
        if (run.Status is JobStatus.Completed or JobStatus.Cancelled)
        {
            return;
        }

        try
        {
            using var runActivity = AppTracing.Source.StartActivity("AgentRun.Execute");
            runActivity?.SetTag("run.id", run.Id);
            runActivity?.SetTag("application.id", run.ApplicationId);

            var stopwatch = Stopwatch.StartNew();
            var outcome = "success";
            try
            {
                await RunPipelineAsync(run, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                outcome = "failure";
                AppMetrics.RunFailures.Add(1, new TagList { { "category", ClassifyException(ex).ToString() } });
                throw;
            }
            finally
            {
                AppMetrics.RunDurationMs.Record(stopwatch.Elapsed.TotalMilliseconds, new TagList { { "outcome", outcome } });
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await HandleFailureAsync(run, message, ex, cancellationToken);
        }
    }

    private async Task RunPipelineAsync(AgentRun run, CancellationToken cancellationToken)
    {
        // Preparing: parse the run's snapshots and validate the deployment +
        // context budget before touching retrieval or AOAI (ASDD §16 order).
        run.UpdateProgress(RunStage.Preparing, 5, timeProvider.GetUtcNow());
        await agentRunRepository.SaveChangesAsync(cancellationToken);
        await PublishRunStageChangedAsync(run, cancellationToken);

        var requestedParameters = Deserialize<AgentRunParametersSnapshot>(run.RequestedParametersJson, "requested parameters");
        var retrievalSettings = Deserialize<AgentRunRetrievalSettingsSnapshot>(run.RetrievalSettingsJson, "retrieval settings");
        var deploymentName = run.DeploymentSnapshot
            ?? throw new AgentRunExecutionException(ErrorCategory.Configuration, $"Run '{run.Id}' has no deployment snapshot recorded.");

        ContextBudgetResult budget;
        using (AppTracing.Source.StartActivity("Calculate budget"))
        {
            budget = contextBudgetCalculator.Calculate(new ContextBudgetRequest(
                PromptAssembler.PlatformInstructions,
                run.AgentPromptSnapshot,
                run.UserRequest,
                PromptAssembler.ProtocolInstructions,
                requestedParameters.MaximumOutputTokens,
                retrievalSettings.MaximumEvidenceTokens,
                deploymentName));
        }

        AppMetrics.ContextTokenUsage.Record(budget.FixedLayerTokens, new TagList { { "layer", "fixed" } });
        AppMetrics.ContextUtilization.Record((double)budget.FixedLayerTokens / budget.DeploymentContextTokens);

        // Retrieving
        run.UpdateProgress(RunStage.Retrieving, 20, timeProvider.GetUtcNow());
        await agentRunRepository.SaveChangesAsync(cancellationToken);
        await PublishRunStageChangedAsync(run, cancellationToken);

        var selectedFileIds = string.IsNullOrEmpty(run.KnowledgeScopeSnapshotJson)
            ? null
            : JsonSerializer.Deserialize<List<Guid>>(run.KnowledgeScopeSnapshotJson, JsonOptions);

        var retrievalMode = Enum.Parse<RetrievalMode>(retrievalSettings.RetrievalMode);
        var retrievalRequest = new RetrievalRequest(
            run.ApplicationId,
            run.UserRequest,
            new RetrievalSettings(
                retrievalSettings.RetrievalCount,
                retrievalSettings.MinimumSimilarity,
                DefaultSemanticWeight,
                DefaultKeywordWeight,
                retrievalSettings.IncludeNeighbors,
                budget.EvidenceTokenBudget,
                retrievalSettings.StrictGrounding,
                retrievalMode),
            selectedFileIds);

        var retrievalResult = await retrievalService.RetrieveAsync(retrievalRequest, cancellationToken);

        if (retrievalResult.NoEvidence && retrievalSettings.StrictGrounding)
        {
            throw new AgentRunExecutionException(
                ErrorCategory.RetrievalEmpty,
                "No evidence matched this request and strict grounding is enabled for this agent.");
        }

        var runEvidence = await runEvidenceService.PersistAsync(
            run.ApplicationId, run.Id, retrievalResult.Sources, timeProvider.GetUtcNow(), cancellationToken);

        // BuildingContext
        run.UpdateProgress(RunStage.BuildingContext, 40, timeProvider.GetUtcNow());
        await agentRunRepository.SaveChangesAsync(cancellationToken);
        await PublishRunStageChangedAsync(run, cancellationToken);

        AssembledPrompt assembledPrompt;
        using (AppTracing.Source.StartActivity("Evidence assembly"))
        {
            assembledPrompt = PromptAssembler.Assemble(
                new PromptAssemblyRequest(run.AgentPromptSnapshot, run.UserRequest, retrievalResult.Sources));
        }

        // TASK-RUN-05: last pre-generation checkpoint - a cancel request made
        // while this run was Preparing/Retrieving/BuildingContext only sets a
        // flag (a concurrent write from the API, safe alongside this
        // instance's own tracked changes); check the freshly-persisted value
        // here, before ever calling AOAI, and honor it if set.
        if (await agentRunRepository.IsCancellationRequestedAsync(run.Id, cancellationToken))
        {
            run.Cancel(timeProvider.GetUtcNow());
            await agentRunRepository.SaveChangesAsync(cancellationToken);
            await PublishRunCancelledAsync(run, cancellationToken);
            LogRunCancelled(run.Id);
            return;
        }

        // Generating
        run.UpdateProgress(RunStage.Generating, 60, timeProvider.GetUtcNow());
        await agentRunRepository.SaveChangesAsync(cancellationToken);
        await PublishRunStageChangedAsync(run, cancellationToken);

        GenerationOutcome generation;
        using (AppTracing.Source.StartActivity("Azure OpenAI generation"))
        {
            generation = await generationService.GenerateAsync(run, assembledPrompt, cancellationToken);
        }
        // GenerationService.GenerateAsync already called run.RecordEffectiveParameters(...) in-memory.
        await agentRunRepository.SaveChangesAsync(cancellationToken);

        // Validating
        run.UpdateProgress(RunStage.Validating, 80, timeProvider.GetUtcNow());
        await agentRunRepository.SaveChangesAsync(cancellationToken);
        await PublishRunStageChangedAsync(run, cancellationToken);

        ValidatedMarkdownOutput validated;
        using (AppTracing.Source.StartActivity("Markdown validation"))
        {
            validated = MarkdownOutputValidator.ValidateAndExtract(generation.Content);
        }

        // Enforce the agent's CitationsRequired policy (ASDD §17.3) before any
        // Saving-stage write, so a policy failure never leaves a half-saved
        // output behind - re-validated for real (and persisted) below.
        CitationMarkerValidator.CitationValidationResult precheck;
        using (AppTracing.Source.StartActivity("Citation validation"))
        {
            precheck = CitationMarkerValidator.Validate(
                run.ApplicationId, Guid.Empty, validated.CitationMarkers, runEvidence, timeProvider.GetUtcNow());
        }
        if (retrievalSettings.CitationsRequired && precheck.Citations.Count == 0)
        {
            throw new AgentRunExecutionException(
                ErrorCategory.OutputValidationFailure,
                "This agent requires citations, but the generated output contained no valid citation markers.");
        }

        // Saving
        run.UpdateProgress(RunStage.Saving, 90, timeProvider.GetUtcNow());
        await agentRunRepository.SaveChangesAsync(cancellationToken);
        await PublishRunStageChangedAsync(run, cancellationToken);

        var savingNow = timeProvider.GetUtcNow();
        using (AppTracing.Source.StartActivity("Persist output"))
        {
            var output = await generatedOutputRepository.GetByAgentRunAsync(run.Id, cancellationToken);
            if (output is null)
            {
                output = GeneratedOutput.Create(
                    run.ApplicationId,
                    run.Id,
                    run.OutputTitle ?? DeriveDefaultTitle(run.UserRequest),
                    validated.OriginalMarkdown,
                    run.RequestedBy,
                    savingNow);
                await generatedOutputRepository.AddAsync(output, cancellationToken);
                await generatedOutputRepository.SaveChangesAsync(cancellationToken);
            }

            await citationService.ValidateAndPersistAsync(
                run.ApplicationId, output.Id, validated.CitationMarkers, runEvidence, savingNow, cancellationToken);
        }

        run.Complete(timeProvider.GetUtcNow());
        await agentRunRepository.SaveChangesAsync(cancellationToken);
        await PublishRunCompletedAsync(run, cancellationToken);
    }

    private async Task HandleFailureAsync(
        AgentRun run,
        AgentExecutionMessage message,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var category = ClassifyException(exception);
        var now = timeProvider.GetUtcNow();

        if (RetryClassifier.ShouldAutoRetry(category, run.AttemptNumber))
        {
            var retryMessage = message with { MessageId = Guid.NewGuid(), AttemptNumber = run.AttemptNumber + 1 };
            var outboxRow = OutboxMessage.Create(
                OutboxMessageType.AgentExecution, JsonSerializer.Serialize(retryMessage, JsonOptions), now);
            await outboxRepository.AddAsync(outboxRow, cancellationToken);
            await outboxRepository.SaveChangesAsync(cancellationToken);

            // RequeueForRetry requires the run to be in the Failed status
            // (it is also the manual-retry entry point, TASK-JOB-06) - fail
            // it first so this automatic retry satisfies that precondition.
            run.Fail(category, exception.Message, now);
            run.RequeueForRetry(outboxRow.Id, now);
            await agentRunRepository.SaveChangesAsync(cancellationToken);

            LogRetryableFailure(exception, run.Id, category, run.AttemptNumber);
            return;
        }

        run.Fail(category, exception.Message, now);
        await agentRunRepository.SaveChangesAsync(cancellationToken);
        await PublishRunFailedAsync(run, category, cancellationToken);

        LogTerminalFailure(exception, run.Id, category);
    }

    private static ErrorCategory ClassifyException(Exception exception) => exception switch
    {
        AgentRunExecutionException agentEx => agentEx.Category,
        TimeoutException => ErrorCategory.Timeout,
        _ => ErrorCategory.InternalFailure,
    };

    // TASK-SIG-03: published post-commit only, mirroring DocumentProcessingOrchestrator's
    // convention - see that type's equivalent helpers for the full rationale
    // (best-effort delivery, worker-process HTTP-relay publisher, etc.).
    private Task PublishRunStageChangedAsync(AgentRun run, CancellationToken cancellationToken)
    {
        var envelope = new SignalREvent<RunStageChangedPayload>(
            Guid.NewGuid(), RealtimeEventTypes.RunStageChanged, run.ApplicationId, run.Id,
            timeProvider.GetUtcNow(), run.EventSequence, new RunStageChangedPayload(run.Id, run.Stage, run.Progress));
        return realtimePublisher.PublishAsync(
            RealtimeEventTypes.RunStageChanged,
            [RealtimeGroups.Run(run.Id), RealtimeGroups.Application(run.ApplicationId)],
            envelope,
            cancellationToken);
    }

    private Task PublishRunCancelledAsync(AgentRun run, CancellationToken cancellationToken)
    {
        var envelope = new SignalREvent<RunCancelledPayload>(
            Guid.NewGuid(), RealtimeEventTypes.RunCancelled, run.ApplicationId, run.Id,
            timeProvider.GetUtcNow(), run.EventSequence, new RunCancelledPayload(run.Id));
        return realtimePublisher.PublishAsync(
            RealtimeEventTypes.RunCancelled,
            [RealtimeGroups.Run(run.Id), RealtimeGroups.Application(run.ApplicationId)],
            envelope,
            cancellationToken);
    }

    private async Task PublishRunCompletedAsync(AgentRun run, CancellationToken cancellationToken)
    {
        var envelope = new SignalREvent<RunCompletedPayload>(
            Guid.NewGuid(), RealtimeEventTypes.RunCompleted, run.ApplicationId, run.Id,
            timeProvider.GetUtcNow(), run.EventSequence, new RunCompletedPayload(run.Id));
        await realtimePublisher.PublishAsync(
            RealtimeEventTypes.RunCompleted,
            [RealtimeGroups.Run(run.Id), RealtimeGroups.Application(run.ApplicationId)],
            envelope,
            cancellationToken);
        await PublishDashboardChangedAsync(run.ApplicationId, RealtimeEventTypes.RunCompleted, cancellationToken);
    }

    private async Task PublishRunFailedAsync(AgentRun run, ErrorCategory category, CancellationToken cancellationToken)
    {
        var envelope = new SignalREvent<RunFailedPayload>(
            Guid.NewGuid(), RealtimeEventTypes.RunFailed, run.ApplicationId, run.Id,
            timeProvider.GetUtcNow(), run.EventSequence, new RunFailedPayload(run.Id, category));
        await realtimePublisher.PublishAsync(
            RealtimeEventTypes.RunFailed,
            [RealtimeGroups.Run(run.Id), RealtimeGroups.Application(run.ApplicationId)],
            envelope,
            cancellationToken);
        await PublishDashboardChangedAsync(run.ApplicationId, RealtimeEventTypes.RunFailed, cancellationToken);
    }

    private Task PublishDashboardChangedAsync(Guid applicationId, string reason, CancellationToken cancellationToken)
    {
        var envelope = new SignalREvent<DashboardChangedPayload>(
            Guid.NewGuid(), RealtimeEventTypes.DashboardChanged, applicationId, applicationId,
            timeProvider.GetUtcNow(), Sequence: 0, new DashboardChangedPayload(reason));
        return realtimePublisher.PublishAsync(
            RealtimeEventTypes.DashboardChanged,
            [RealtimeGroups.Application(applicationId), RealtimeGroups.OperationsGroup],
            envelope,
            cancellationToken);
    }

    private static string DeriveDefaultTitle(string userRequest)
    {
        var trimmed = userRequest.Trim();
        return trimmed.Length <= MaxTitleLength ? trimmed : string.Concat(trimmed.AsSpan(0, MaxTitleLength).ToString().TrimEnd(), "\u2026");
    }

    private static T Deserialize<T>(string? json, string description)
    {
        if (string.IsNullOrEmpty(json))
        {
            throw new AgentRunExecutionException(ErrorCategory.Configuration, $"Run has no {description} recorded.");
        }

        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new AgentRunExecutionException(ErrorCategory.Configuration, $"Run has malformed {description}.");
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Agent run '{RunId}' referenced by message '{MessageId}' was not found.")]
    partial void LogRunNotFound(Guid runId, Guid messageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Agent run '{RunId}' was cancelled before generation started.")]
    partial void LogRunCancelled(Guid runId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Retryable failure for run '{RunId}' ({Category}); requeued as attempt {AttemptNumber}.")]
    partial void LogRetryableFailure(Exception exception, Guid runId, ErrorCategory category, int attemptNumber);

    [LoggerMessage(Level = LogLevel.Error, Message = "Terminal failure for run '{RunId}' ({Category}).")]
    partial void LogTerminalFailure(Exception exception, Guid runId, ErrorCategory category);
}
