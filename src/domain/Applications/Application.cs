using EnterpriseKnowledgeAgent.Shared.Applications;

namespace EnterpriseKnowledgeAgent.Domain.Applications;

/// <summary>
/// TASK-APP-01: the <c>Application</c> aggregate (FR-APP-001, FR-APP-002,
/// ASDD §9). Applications never hard-delete; <see cref="Delete"/> moves them
/// to a terminal soft-delete state so historical outputs remain referenceable
/// (FR-CIT-004). All state changes go through guarded methods here rather
/// than public setters, so invalid transitions are impossible to express.
/// </summary>
public sealed class Application
{
    /// <summary>Maximum length enforced for <see cref="Name"/> (mirrors the EF configuration's column length).</summary>
    public const int MaxNameLength = 200;

    /// <summary>Maximum length enforced for <see cref="Description"/> (mirrors the EF configuration's column length).</summary>
    public const int MaxDescriptionLength = 2000;

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public ApplicationStatus Status { get; private set; }

    /// <summary>FR-APP-001/TASK-APP-06: when <see langword="true"/>, ApplicationViewers may run active agents in this application.</summary>
    public bool AllowViewerExecution { get; private set; }

    /// <summary>Keycloak subject (<c>sub</c>) of the SuperAdmin who created this application (decision D20 - opaque string, not a foreign key).</summary>
    public string CreatedBy { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public DateTimeOffset? ArchivedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>Archived or deleted applications reject state-changing operations everywhere else in the platform (TASK-APP-04's shared read-only guard).</summary>
    public bool IsReadOnly => Status is ApplicationStatus.Archived or ApplicationStatus.Deleted;

#pragma warning disable CS8618 // required by EF Core materialization; all properties are always populated via Create/mapping.
    private Application()
    {
    }
#pragma warning restore CS8618

    public static Application Create(string name, string? description, string createdBy, DateTimeOffset now, bool allowViewerExecution = false)
    {
        var normalizedName = NormalizeName(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);

        return new Application
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            Description = NormalizeDescription(description),
            Status = ApplicationStatus.Active,
            AllowViewerExecution = allowViewerExecution,
            CreatedBy = createdBy,
            CreatedAt = now,
        };
    }

    /// <summary>Edits name/description. Rejected once the application is deleted (a terminal state); archived applications may still be renamed ahead of a restore.</summary>
    public void Update(string name, string? description, DateTimeOffset now)
    {
        ThrowIfDeleted();

        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        UpdatedAt = now;
    }

    /// <summary>TASK-APP-06: toggles whether ApplicationViewers may run active agents.</summary>
    public void SetAllowViewerExecution(bool allow, DateTimeOffset now)
    {
        ThrowIfDeleted();

        AllowViewerExecution = allow;
        UpdatedAt = now;
    }

    public void Archive(DateTimeOffset now)
    {
        if (Status != ApplicationStatus.Active)
        {
            throw new ApplicationStateConflictException(
                $"Application '{Id}' must be Active to archive (current status: {Status}).");
        }

        Status = ApplicationStatus.Archived;
        ArchivedAt = now;
        UpdatedAt = now;
    }

    public void Restore(DateTimeOffset now)
    {
        if (Status != ApplicationStatus.Archived)
        {
            throw new ApplicationStateConflictException(
                $"Application '{Id}' must be Archived to restore (current status: {Status}).");
        }

        Status = ApplicationStatus.Active;
        ArchivedAt = null;
        UpdatedAt = now;
    }

    /// <summary>Idempotent (TASK-APP-02): deleting an already-deleted application is a no-op, not an error.</summary>
    public void Delete(DateTimeOffset now)
    {
        if (Status == ApplicationStatus.Deleted)
        {
            return;
        }

        Status = ApplicationStatus.Deleted;
        DeletedAt = now;
        UpdatedAt = now;
    }

    private void ThrowIfDeleted()
    {
        if (Status == ApplicationStatus.Deleted)
        {
            throw new ApplicationStateConflictException($"Application '{Id}' is deleted and can no longer be edited.");
        }
    }

    private static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)
        {
            throw new ArgumentException($"Name must be at most {MaxNameLength} characters.", nameof(name));
        }

        return trimmed;
    }

    private static string? NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var trimmed = description.Trim();
        if (trimmed.Length > MaxDescriptionLength)
        {
            throw new ArgumentException($"Description must be at most {MaxDescriptionLength} characters.", nameof(description));
        }

        return trimmed;
    }
}
