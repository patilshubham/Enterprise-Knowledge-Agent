using EnterpriseKnowledgeAgent.Application.AzureOpenAI;
using EnterpriseKnowledgeAgent.Application.Citations;
using EnterpriseKnowledgeAgent.Application.Jobs;
using EnterpriseKnowledgeAgent.Application.Knowledge;
using EnterpriseKnowledgeAgent.Application.Outputs;
using EnterpriseKnowledgeAgent.Application.Retrieval;
using EnterpriseKnowledgeAgent.Application.Tokenization;
using EnterpriseKnowledgeAgent.Domain.Citations;
using EnterpriseKnowledgeAgent.Domain.Execution;
using EnterpriseKnowledgeAgent.Domain.Knowledge;
using EnterpriseKnowledgeAgent.Domain.Operations;
using EnterpriseKnowledgeAgent.Domain.Outputs;
using EnterpriseKnowledgeAgent.Shared.Jobs;
using EnterpriseKnowledgeAgent.Shared.Knowledge;

namespace EnterpriseKnowledgeAgent.Application.Tests.Execution;

/// <summary>Minimal in-memory fakes for <see cref="AgentRunOrchestrator"/>'s many dependencies - just enough behavior to drive TASK-RUN-02 scenarios.</summary>
internal sealed class FakeAgentRunRepositoryForOrchestrator : IAgentRunRepository
{
    public readonly Dictionary<Guid, AgentRun> Runs = [];

    public Task AddAsync(AgentRun run, CancellationToken cancellationToken = default)
    {
        Runs[run.Id] = run;
        return Task.CompletedTask;
    }

    public Task<AgentRun?> GetAsync(Guid runId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Runs.GetValueOrDefault(runId));

    public Task<AgentRun?> GetByIdempotencyKeyAsync(Guid agentId, string idempotencyKey, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not exercised by orchestrator tests.");

    public Task<bool> TryClaimAsync(Guid runId, string workerId, DateTimeOffset now, TimeSpan leaseDuration, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not exercised by orchestrator tests - claim happens before ProcessAsync is called.");

    public Task<bool> IsCancellationRequestedAsync(Guid runId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Runs.TryGetValue(runId, out var run) && run.CancellationRequested);

    public Task<bool> HeartbeatAsync(Guid runId, string workerId, DateTimeOffset now, TimeSpan leaseExtension, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not exercised by orchestrator tests.");

    public Task<IReadOnlyList<AgentRun>> ListExpiredLeasesAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AgentRun>>([]);

    public Task<IReadOnlyList<AgentRun>> ListStaleUndeliveredAsync(DateTimeOffset createdBefore, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AgentRun>>([]);

    public Task<IReadOnlyList<AgentRun>> ListFailedAsync(Guid applicationId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AgentRun>>([]);

    public Task<IReadOnlyDictionary<JobStatus, int>> CountByStatusAsync(Guid? applicationId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not exercised by orchestrator tests.");

    public Task<IReadOnlyList<AgentRun>> ListAsync(
        Guid applicationId, JobStatus? status = null, Guid? agentId = null, string? createdBy = null, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not exercised by orchestrator tests.");

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class FakeOutboxRepositoryForOrchestrator : IOutboxRepository
{
    public readonly List<OutboxMessage> Messages = [];

    public Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        Messages.Add(message);
        return Task.CompletedTask;
    }

    public Task<OutboxMessage?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Messages.FirstOrDefault(m => m.Id == id));

    public Task<IReadOnlyList<OutboxMessage>> ListUndeliveredAsync(int batchSize, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<OutboxMessage>>([]);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class FakeKnowledgeFileRepositoryForOrchestrator : IKnowledgeFileRepository
{
    public readonly Dictionary<Guid, KnowledgeFile> Files = [];

    public Task AddAsync(KnowledgeFile file, CancellationToken cancellationToken = default)
    {
        Files[file.Id] = file;
        return Task.CompletedTask;
    }

    public Task<KnowledgeFile?> GetAsync(Guid fileId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Files.GetValueOrDefault(fileId));

    public Task<IReadOnlyList<KnowledgeFile>> ListAsync(
        Guid applicationId, string? search = null, UserFacingFileStatus? status = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<KnowledgeFile>>([]);

    public Task<bool> ExistsActiveHashAsync(Guid applicationId, string contentHash, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<IReadOnlyDictionary<UserFacingFileStatus, int>> CountByStatusAsync(Guid? applicationId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not exercised by orchestrator tests.");

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class FakeRunEvidenceRepositoryForOrchestrator : IRunEvidenceRepository
{
    public readonly List<RunEvidence> Evidence = [];

    public Task<IReadOnlyList<RunEvidence>> ListByRunAsync(Guid agentRunId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RunEvidence>>(Evidence.Where(e => e.AgentRunId == agentRunId).ToList());

    public Task AddRangeAsync(IReadOnlyList<RunEvidence> evidence, CancellationToken cancellationToken = default)
    {
        Evidence.AddRange(evidence);
        return Task.CompletedTask;
    }
}

internal sealed class FakeCitationRepositoryForOrchestrator : ICitationRepository
{
    public readonly List<Citation> Citations = [];

    public Task<IReadOnlyList<Citation>> ListByOutputAsync(Guid generatedOutputId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Citation>>(Citations.Where(c => c.GeneratedOutputId == generatedOutputId).ToList());

    public Task AddRangeAsync(IReadOnlyList<Citation> citations, CancellationToken cancellationToken = default)
    {
        Citations.AddRange(citations);
        return Task.CompletedTask;
    }
}

internal sealed class FakeGeneratedOutputRepository : IGeneratedOutputRepository
{
    public readonly Dictionary<Guid, GeneratedOutput> Outputs = [];

    public Task AddAsync(GeneratedOutput output, CancellationToken cancellationToken = default)
    {
        Outputs[output.Id] = output;
        return Task.CompletedTask;
    }

    public Task<GeneratedOutput?> GetAsync(Guid outputId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Outputs.GetValueOrDefault(outputId));

    public Task<GeneratedOutput?> GetByAgentRunAsync(Guid agentRunId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Outputs.Values.FirstOrDefault(o => o.AgentRunId == agentRunId));

    public Task<IReadOnlyList<GeneratedOutput>> ListAsync(
        Guid applicationId,
        string? search = null,
        Guid? agentId = null,
        string? createdBy = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        JobStatus? runStatus = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<GeneratedOutput>>(
            Outputs.Values.Where(o => o.ApplicationId == applicationId && !o.IsDeleted).ToList());

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class FakeRetrievalServiceForOrchestrator : IRetrievalService
{
    public RetrievalResult Result { get; set; } = new(NoEvidence: true, Sources: [], ExcludedFileIds: []);

    public Exception? ThrowOnRetrieve { get; set; }

    public RetrievalRequest? LastRequest { get; private set; }

    public Task<RetrievalResult> RetrieveAsync(RetrievalRequest request, CancellationToken cancellationToken = default)
    {
        if (ThrowOnRetrieve is not null)
        {
            throw ThrowOnRetrieve;
        }

        LastRequest = request;
        return Task.FromResult(Result);
    }
}

internal sealed class FakeTextGenerationServiceForOrchestrator : ITextGenerationService
{
    public TextGenerationResult Result { get; set; } = new("Generated Markdown.", "stop");

    public Exception? ThrowOnGenerate { get; set; }

    /// <summary>TASK-RUN-05: lets cancellation-checkpoint tests assert generation was never invoked.</summary>
    public int CallCount { get; private set; }

    public Task<TextGenerationResult> GenerateAsync(IReadOnlyList<ChatMessage> messages, string deployment, GenerationParameters? parameters = null, CancellationToken cancellationToken = default)
    {
        CallCount++;
        if (ThrowOnGenerate is not null)
        {
            throw ThrowOnGenerate;
        }

        return Task.FromResult(Result);
    }

    public async IAsyncEnumerable<string> GenerateStreamingAsync(IReadOnlyList<ChatMessage> messages, string deployment, GenerationParameters? parameters = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        CallCount++;
        if (ThrowOnGenerate is not null)
        {
            throw ThrowOnGenerate;
        }

        await Task.CompletedTask;
        yield return Result.Content;
    }

    public Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
}

internal sealed class FakeDeploymentRegistryForOrchestrator : IDeploymentRegistry
{
    public const string ChatDeployment = "gpt-4o";

    public int? MaxContextTokens { get; set; } = 128_000;

    public IReadOnlyList<DeploymentInfo> ListDeployments() =>
    [
        new DeploymentInfo(ChatDeployment, DeploymentPurpose.Chat, MaxContextTokens, 4_096, EmbeddingDimensions: null, SupportsTopP: true),
    ];
}

/// <summary>One token per character, so tests can reason about exact numbers.</summary>
internal sealed class FakeTokenEstimatorForOrchestrator : ITokenEstimator
{
    public int EstimateTokenCount(string text, string? deployment = null) => text.Length;
}
