namespace EnterpriseKnowledgeAgent.Domain.Outputs;

/// <summary>
/// TASK-OUT-01: the persisted, consumable product of one completed
/// <see cref="EnterpriseKnowledgeAgent.Domain.Execution.AgentRun"/> (ASDD
/// §9.5) - stores the original generated Markdown immutably alongside a
/// separately-edited current version (FR-OUT-004: editing never overwrites
/// <see cref="OriginalMarkdown"/>). One-to-one with its run (module 19's
/// TASK-RUN-02 persists this at the run's Saving stage); later views/edits
/// (TASK-OUT-02..05) operate on this entity.
/// </summary>
public sealed class GeneratedOutput
{
    public Guid Id { get; private set; }

    public Guid ApplicationId { get; private set; }

    public Guid AgentRunId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string OriginalMarkdown { get; private set; } = string.Empty;

    public string CurrentMarkdown { get; private set; } = string.Empty;

    public bool IsEdited { get; private set; }

    public bool IsDeleted { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public string? LastEditedBy { get; private set; }

    public DateTimeOffset? LastEditedAt { get; private set; }

#pragma warning disable CS8618 // required by EF Core materialization; all properties are always populated via Create/mapping.
    private GeneratedOutput()
    {
    }
#pragma warning restore CS8618

    public static GeneratedOutput Create(
        Guid applicationId,
        Guid agentRunId,
        string title,
        string originalMarkdown,
        string createdBy,
        DateTimeOffset now) => new()
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            AgentRunId = agentRunId,
            Title = title,
            OriginalMarkdown = originalMarkdown,
            CurrentMarkdown = originalMarkdown,
            IsEdited = false,
            IsDeleted = false,
            CreatedBy = createdBy,
            CreatedAt = now,
        };

    /// <summary>
    /// TASK-OUT-03: admin edit of the current Markdown (FR-OUT-004).
    /// <see cref="OriginalMarkdown"/> is never touched - only <see cref="CurrentMarkdown"/>
    /// moves, and <see cref="IsEdited"/> latches to <see langword="true"/> permanently
    /// once any edit is saved (it never reverts to <see langword="false"/>, even if a
    /// later edit happens to restore the original text verbatim).
    /// </summary>
    public void Edit(string currentMarkdown, string editedBy, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentMarkdown);
        ArgumentException.ThrowIfNullOrWhiteSpace(editedBy);

        CurrentMarkdown = currentMarkdown;
        IsEdited = true;
        LastEditedBy = editedBy;
        LastEditedAt = now;
    }

    /// <summary>
    /// TASK-OUT-04: admin soft-delete (FR-OUT-005). Idempotent by design - calling
    /// this on an already-deleted output is a harmless no-op, so a duplicate
    /// delete request never throws. Citations/evidence are untouched (they
    /// belong to the immutable run record, not the output).
    /// </summary>
    public void SoftDelete() => IsDeleted = true;
}
