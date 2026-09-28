namespace EnterpriseKnowledgeAgent.Shared.Execution;

/// <summary>
/// Agent-run stages (FR-RUN-003). <see cref="Failed"/> and <see cref="Cancelled"/>
/// are terminal alternatives that can be reached from any non-terminal stage.
/// Only pre-generation cancellation is supported in the baseline (Decision D2).
/// </summary>
public enum RunStage
{
    Queued,
    Preparing,
    Retrieving,
    BuildingContext,
    Generating,
    Validating,
    Saving,
    Completed,
    Failed,
    Cancelled,
}
