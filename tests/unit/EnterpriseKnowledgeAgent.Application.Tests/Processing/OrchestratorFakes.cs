using EnterpriseKnowledgeAgent.Application.Chunking;
using EnterpriseKnowledgeAgent.Application.Embeddings;
using EnterpriseKnowledgeAgent.Application.Jobs;
using EnterpriseKnowledgeAgent.Application.Knowledge;
using EnterpriseKnowledgeAgent.Application.Processing;
using EnterpriseKnowledgeAgent.Application.Storage;
using EnterpriseKnowledgeAgent.Domain.Jobs;
using EnterpriseKnowledgeAgent.Domain.Knowledge;
using EnterpriseKnowledgeAgent.Domain.Operations;
using EnterpriseKnowledgeAgent.Shared.Jobs;
using EnterpriseKnowledgeAgent.Shared.Knowledge;

namespace EnterpriseKnowledgeAgent.Application.Tests.Processing;

/// <summary>Minimal in-memory fakes for the orchestrator's repository/storage dependencies - just enough behavior to drive TASK-PRC-01..04 scenarios.</summary>
internal sealed class FakeProcessingJobRepository : IProcessingJobRepository
{
    public readonly Dictionary<Guid, ProcessingJob> Jobs = [];

    public Task AddAsync(ProcessingJob job, CancellationToken cancellationToken = default)
    {
        Jobs[job.Id] = job;
        return Task.CompletedTask;
    }

    public Task<ProcessingJob?> GetAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Jobs.GetValueOrDefault(jobId));

    public Task<ProcessingJob?> GetLatestByFileAsync(Guid fileId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Jobs.Values.Where(j => j.FileId == fileId).MaxBy(j => j.CreatedAt));

    public Task<bool> TryClaimAsync(Guid jobId, string workerId, DateTimeOffset now, TimeSpan leaseDuration, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not exercised by orchestrator tests - claim happens before ProcessAsync is called.");

    public Task<bool> HeartbeatAsync(Guid jobId, string workerId, DateTimeOffset now, TimeSpan leaseExtension, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not exercised by orchestrator tests.");

    public Task<IReadOnlyList<ProcessingJob>> ListExpiredLeasesAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ProcessingJob>>([]);

    public Task<IReadOnlyList<ProcessingJob>> ListStaleUndeliveredAsync(DateTimeOffset createdBefore, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ProcessingJob>>([]);

    public Task<IReadOnlyList<ProcessingJob>> ListFailedAsync(Guid applicationId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ProcessingJob>>([]);

    public Task<IReadOnlyDictionary<JobStatus, int>> CountByStatusAsync(Guid? applicationId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not exercised by orchestrator tests.");

    public Task<IReadOnlyList<ProcessingJob>> ListAsync(
        Guid? applicationId, JobStatus? status = null, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not exercised by orchestrator tests.");

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class FakeKnowledgeFileRepository : IKnowledgeFileRepository
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

internal sealed class FakeKnowledgeChunkRepository : IKnowledgeChunkRepository
{
    public readonly List<KnowledgeChunk> Chunks = [];
    public readonly HashSet<Guid> ActivatedFileIds = [];

    public Task AddRangeAsync(IEnumerable<KnowledgeChunk> chunks, CancellationToken cancellationToken = default)
    {
        Chunks.AddRange(chunks);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<KnowledgeChunk>> ListByFileAsync(Guid fileId, bool activeOnly = false, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<KnowledgeChunk>>(Chunks.Where(c => c.FileId == fileId && (!activeOnly || c.IsActive)).ToList());

    public Task<IReadOnlySet<string>> ListContentHashesByFileAsync(Guid fileId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlySet<string>>(Chunks.Where(c => c.FileId == fileId && !c.IsActive).Select(c => c.ContentHash).ToHashSet());

    public Task ActivateAllForFileAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        ActivatedFileIds.Add(fileId);
        foreach (var chunk in Chunks.Where(c => c.FileId == fileId))
        {
            chunk.Activate();
        }

        return Task.CompletedTask;
    }

    public Task DeleteAllForFileAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        Chunks.RemoveAll(c => c.FileId == fileId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<KnowledgeChunkSearchResult>> SearchByCosineSimilarityAsync(
        Guid applicationId, Pgvector.Vector queryEmbedding, int topK, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<KnowledgeChunkSearchResult>>([]);

    public readonly HashSet<Guid> SwappedFileIds = [];

    public Task DeleteInactiveForFileAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        Chunks.RemoveAll(c => c.FileId == fileId && !c.IsActive);
        return Task.CompletedTask;
    }

    public Task SwapReplacementAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        SwappedFileIds.Add(fileId);
        Chunks.RemoveAll(c => c.FileId == fileId && c.IsActive);
        foreach (var chunk in Chunks.Where(c => c.FileId == fileId && !c.IsActive))
        {
            chunk.Activate();
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlySet<string>> ListDistinctEmbeddingDeploymentsAsync(Guid applicationId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlySet<string>>(Chunks.Where(c => c.ApplicationId == applicationId && c.IsActive && c.EmbeddingDeployment is not null)
            .Select(c => c.EmbeddingDeployment!).ToHashSet());

    public Task<IReadOnlyList<KnowledgeChunkKeywordSearchResult>> SearchByFullTextAsync(
        Guid applicationId, string query, int topK, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<KnowledgeChunkKeywordSearchResult>>([]);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class FakeOutboxRepository : IOutboxRepository
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

internal sealed class FakeObjectStorage : IObjectStorage
{
    public string? ContentToReturn { get; set; }

    public Exception? ExceptionToThrowOnGet { get; set; }

    public readonly List<string> DeletedKeys = [];

    public Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (ExceptionToThrowOnGet is not null)
        {
            throw ExceptionToThrowOnGet;
        }

        Stream stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ContentToReturn ?? string.Empty));
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        DeletedKeys.Add(key);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task<ObjectStorageHealth> CheckHealthAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new ObjectStorageHealth(true, null));
}

/// <summary>Splits on blank lines into whole-file single-block "chunks" - deterministic and independent of the real chunker/parser for orchestrator-focused tests.</summary>
internal sealed class FakeChunker : IChunker
{
    public IReadOnlyList<ChunkDraft> Chunk(IReadOnlyList<ParsedBlock> blocks) =>
        blocks.Select(b => new ChunkDraft(b.HeadingPath, b.Text, 1, b.CharacterStart, b.CharacterEnd)).ToList();
}

internal sealed class FakeEmbeddingPipelineStage : IEmbeddingPipelineStage
{
    public readonly List<(Guid ApplicationId, Guid FileId, bool ActivateOnCompletion)> Calls = [];

    /// <summary>Set to simulate the real stage reporting batch progress before completing.</summary>
    public double[] ProgressFractionsToReport { get; set; } = [];

    public async Task RunAsync(
        Guid applicationId,
        Guid fileId,
        Func<double, CancellationToken, Task>? onProgress = null,
        bool activateOnCompletion = true,
        CancellationToken cancellationToken = default)
    {
        Calls.Add((applicationId, fileId, activateOnCompletion));
        foreach (var fraction in ProgressFractionsToReport)
        {
            if (onProgress is not null)
            {
                await onProgress(fraction, cancellationToken);
            }
        }
    }
}
