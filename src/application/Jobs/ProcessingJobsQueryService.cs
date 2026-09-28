using EnterpriseKnowledgeAgent.Application.Knowledge;
using EnterpriseKnowledgeAgent.Shared.Jobs;

namespace EnterpriseKnowledgeAgent.Application.Jobs;

/// <summary>
/// TASK-PRDSH-01/02: the application-layer orchestration behind the
/// platform-wide and per-application processing dashboard's list/detail
/// endpoints - mirrors <c>OutputsService</c>'s convention (Api layer only
/// maps HTTP to these calls and maps thrown exceptions to Problem Details).
/// </summary>
public sealed class ProcessingJobsQueryService(
    IProcessingJobRepository processingJobRepository,
    IKnowledgeFileRepository knowledgeFileRepository)
{
    private const string DeletedFilePlaceholder = "(deleted file)";

    /// <summary><paramref name="applicationId"/> null = platform-wide (TASK-PRDSH-01); non-null = scoped to one application (TASK-PRDSH-02).</summary>
    public async Task<IReadOnlyList<ProcessingJobSummaryResponse>> ListAsync(
        Guid? applicationId,
        JobStatus? status,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        var jobs = await processingJobRepository.ListAsync(applicationId, status, from, to, cancellationToken);
        if (jobs.Count == 0)
        {
            return [];
        }

        var fileNames = await ResolveFileNamesAsync(jobs.Select(j => j.FileId).Distinct(), cancellationToken);
        return jobs
            .Select(job => ProcessingJobSummaryResponse.FromEntity(job, fileNames.GetValueOrDefault(job.FileId, DeletedFilePlaceholder)))
            .ToList();
    }

    /// <summary><paramref name="applicationId"/> null = platform-wide detail lookup; non-null enforces the job belongs to that application (404-masking cross-application access).</summary>
    public async Task<ProcessingJobDetailResponse> GetDetailAsync(
        Guid? applicationId, Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await processingJobRepository.GetAsync(jobId, cancellationToken);
        if (job is null || (applicationId is { } appId && job.ApplicationId != appId))
        {
            throw new ProcessingJobNotFoundException(jobId);
        }

        var file = await knowledgeFileRepository.GetAsync(job.FileId, cancellationToken);
        return ProcessingJobDetailResponse.FromEntity(job, file?.DisplayName ?? DeletedFilePlaceholder);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveFileNamesAsync(IEnumerable<Guid> fileIds, CancellationToken cancellationToken)
    {
        // Sequential, not Task.WhenAll: a single DbContext instance (this
        // service's scope) is not safe for concurrent operations - see
        // Microsoft.EntityFrameworkCore's ConcurrencyDetector.
        var fileNames = new Dictionary<Guid, string>();
        foreach (var fileId in fileIds)
        {
            var file = await knowledgeFileRepository.GetAsync(fileId, cancellationToken);
            fileNames[fileId] = file?.DisplayName ?? DeletedFilePlaceholder;
        }

        return fileNames;
    }
}
