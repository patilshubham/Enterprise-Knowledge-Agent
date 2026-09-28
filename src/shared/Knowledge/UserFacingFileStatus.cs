namespace EnterpriseKnowledgeAgent.Shared.Knowledge;

/// <summary>
/// User-facing knowledge file states (FR-PRC-002). Distinct from the more
/// granular internal <see cref="ProcessingStage"/> the document worker
/// progresses through while a file is in the "Processing" state.
/// </summary>
public enum UserFacingFileStatus
{
    Queued,
    Processing,
    Ready,
    Failed,
    Disabled,
    Deleting,

    /// <summary>TASK-REP-03/module 15: terminal state after the deletion worker finishes cleanup. The record is kept (not hard-deleted) so historical outputs/citations (module 22) can still resolve the file id and render "source unavailable".</summary>
    Deleted,
}
