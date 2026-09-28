namespace EnterpriseKnowledgeAgent.Application.Applications;

/// <summary>TASK-APP-02: request body for <c>POST /api/applications</c>.</summary>
public sealed record CreateApplicationRequest(string Name, string? Description, bool AllowViewerExecution = false);

/// <summary>TASK-APP-02/TASK-APP-06: request body for <c>PUT /api/applications/{id}</c>. <c>AllowViewerExecution</c> is FR-APP-001's per-application "viewers may run agents" flag.</summary>
public sealed record UpdateApplicationRequest(string Name, string? Description, bool AllowViewerExecution);

/// <summary>TASK-APP-02: response shape for every application read/write endpoint.</summary>
public sealed record ApplicationResponse(
    Guid Id,
    string Name,
    string? Description,
    string Status,
    bool AllowViewerExecution,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? ArchivedAt,
    DateTimeOffset? DeletedAt);
