namespace EnterpriseKnowledgeAgent.Shared.Realtime;

/// <summary>
/// TASK-SIG-02: the fixed event-type catalog (ASDD §18.1). Values are the
/// exact SignalR client-method names invoked by the hub relay (module 25) -
/// frontend code registers one handler per constant via
/// <c>connection.on(RealtimeEventTypes.X, ...)</c>.
/// </summary>
public static class RealtimeEventTypes
{
    public const string FileQueued = nameof(FileQueued);
    public const string FileStageChanged = nameof(FileStageChanged);
    public const string FileCompleted = nameof(FileCompleted);
    public const string FileFailed = nameof(FileFailed);

    public const string RunQueued = nameof(RunQueued);
    public const string RunStageChanged = nameof(RunStageChanged);
    public const string RunCompleted = nameof(RunCompleted);
    public const string RunFailed = nameof(RunFailed);

    /// <summary>Not in ASDD §18.1's original list, but a natural extension of it: <c>RunStage.Cancelled</c> (TASK-RUN-05) is a distinct terminal outcome from both Completed and Failed, so the frontend can render the correct final state instead of guessing from a generic stage-changed event.</summary>
    public const string RunCancelled = nameof(RunCancelled);

    /// <summary>Published to <c>application:{id}</c> and/or <c>platform:operations</c> whenever a file/run reaches a terminal or otherwise dashboard-relevant state - carries no data itself; recipients simply invalidate their dashboard queries (ASDD §18.4, REST stays authoritative).</summary>
    public const string DashboardChanged = nameof(DashboardChanged);
}
