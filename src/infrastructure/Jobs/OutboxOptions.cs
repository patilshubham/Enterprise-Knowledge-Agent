namespace EnterpriseKnowledgeAgent.Infrastructure.Jobs;

/// <summary>TASK-JOB-04/05: polling cadence and batch sizing for the outbox publisher and reconciler (bound from the <c>Outbox</c> config section).</summary>
public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>How often the publisher polls for undelivered rows.</summary>
    public int PublisherPollIntervalSeconds { get; init; } = 5;

    /// <summary>Max undelivered rows fetched per publisher poll.</summary>
    public int PublisherBatchSize { get; init; } = 50;

    /// <summary>TASK-JOB-05: how often the reconciler scans for stuck Queued jobs/runs.</summary>
    public int ReconcilerPollIntervalSeconds { get; init; } = 60;

    /// <summary>TASK-JOB-05: a Queued job/run must be at least this old (with an undelivered outbox row) before the reconciler republishes it - keeps the threshold conservative to avoid double-sends racing the publisher's own retry.</summary>
    public int ReconcilerStalenessThresholdSeconds { get; init; } = 120;
}
