namespace EnterpriseKnowledgeAgent.Application.Outputs;

/// <summary>TASK-OUT-05: one row of the output library listing.</summary>
public sealed record OutputSummaryResponse(
    Guid Id,
    string Title,
    Guid AgentId,
    string AgentName,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    bool IsEdited);

/// <summary>
/// TASK-OUT-02: the full output detail view - Markdown (both versions),
/// the owning run/agent/parameter snapshots, and a lightweight citations
/// list (module 22's <see cref="CitationDetailResponse"/> gives the full
/// evidence detail on demand for the evidence drawer).
/// </summary>
public sealed record OutputDetailResponse(
    Guid Id,
    Guid ApplicationId,
    Guid AgentRunId,
    string Title,
    string OriginalMarkdown,
    string CurrentMarkdown,
    bool IsEdited,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    string? LastEditedBy,
    DateTimeOffset? LastEditedAt,
    Guid AgentId,
    string AgentName,
    string? DeploymentSnapshot,
    string? RequestedParametersJson,
    string? EffectiveParametersJson,
    string? RetrievalSettingsJson,
    IReadOnlyList<CitationDetailResponse> Citations);

/// <summary>
/// TASK-CIT-04: one citation's full evidence detail (filename, heading,
/// matching passage, source-document offsets, processing time, and whether
/// the source file has since been deleted - FR-CIT-004).
/// </summary>
public sealed record CitationDetailResponse(
    Guid Id,
    string SourceId,
    string FileName,
    string HeadingPath,
    string Passage,
    int CharacterStart,
    int CharacterEnd,
    DateTimeOffset? ProcessedAt,
    bool SourceUnavailable);

/// <summary>TASK-OUT-03: admin edit request body.</summary>
public sealed record EditOutputRequest(string CurrentMarkdown);

/// <summary>TASK-OUT-04: regenerate response - the newly queued run's id.</summary>
public sealed record RegenerateOutputResponse(Guid RunId);
