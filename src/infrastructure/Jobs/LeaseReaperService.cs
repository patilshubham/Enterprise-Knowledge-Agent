using EnterpriseKnowledgeAgent.Application.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnterpriseKnowledgeAgent.Infrastructure.Jobs;

/// <summary>TASK-JOB-06: how often the reaper scans for expired leases (bound from the <c>Jobs</c> config section).</summary>
public sealed class JobsOptions
{
    public const string SectionName = "Jobs";

    /// <summary>How often the reaper scans for Running jobs/runs whose lease has expired (crashed/stalled worker).</summary>
    public int ReaperPollIntervalSeconds { get; init; } = 30;
}

/// <summary>
/// TASK-JOB-06: periodically scans for processing jobs/agent runs whose
/// lease expired while still Running (the owning worker crashed or stalled
/// without heartbeating) and releases them back to Queued so another
/// worker can claim them.
/// </summary>
public sealed class LeaseReaperService(
    IServiceScopeFactory scopeFactory,
    IOptions<JobsOptions> options,
    ILogger<LeaseReaperService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = TimeSpan.FromSeconds(Math.Max(1, options.Value.ReaperPollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReapOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.ReaperPassFailed(ex);
            }

            try
            {
                await Task.Delay(pollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
        }
    }

    private async Task ReapOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var processingJobRepository = scope.ServiceProvider.GetRequiredService<IProcessingJobRepository>();
        var agentRunRepository = scope.ServiceProvider.GetRequiredService<IAgentRunRepository>();

        var now = DateTimeOffset.UtcNow;

        var expiredJobs = await processingJobRepository.ListExpiredLeasesAsync(now, cancellationToken);
        foreach (var job in expiredJobs)
        {
            job.ReleaseExpiredLease(now);
            logger.ReleasedExpiredJobLease(job.Id, job.WorkerId);
        }

        if (expiredJobs.Count > 0)
        {
            await processingJobRepository.SaveChangesAsync(cancellationToken);
        }

        var expiredRuns = await agentRunRepository.ListExpiredLeasesAsync(now, cancellationToken);
        foreach (var run in expiredRuns)
        {
            run.ReleaseExpiredLease(now);
            logger.ReleasedExpiredRunLease(run.Id, run.WorkerId);
        }

        if (expiredRuns.Count > 0)
        {
            await agentRunRepository.SaveChangesAsync(cancellationToken);
        }
    }
}
