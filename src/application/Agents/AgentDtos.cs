namespace EnterpriseKnowledgeAgent.Application.Agents;

/// <summary>
/// TASK-AGT-02: request body for <c>POST /api/applications/{appId}/agents</c>.
/// Enum-like fields (<see cref="KnowledgeScopeType"/>, <see cref="RetrievalMode"/>)
/// are plain strings on the wire - mirrors <c>ApplicationResponse.Status</c>'s
/// convention since no global string-enum JSON converter is registered.
/// </summary>
public sealed record CreateAgentRequest(
    string Name,
    string? Description,
    string Prompt,
    string KnowledgeScopeType,
    IReadOnlyList<Guid>? SelectedFileIds,
    string GenerationDeployment,
    double Temperature,
    int MaximumOutputTokens,
    double? TopP,
    string RetrievalMode,
    int RetrievalCount,
    double MinimumSimilarity,
    int MaximumEvidenceTokens,
    bool IncludeNeighbors,
    bool StrictGrounding,
    bool CitationsRequired);

/// <summary>TASK-AGT-02/FR-AGT-005: request body for <c>PUT .../agents/{id}</c> - same shape as create; editing overwrites the single current configuration wholesale.</summary>
public sealed record UpdateAgentRequest(
    string Name,
    string? Description,
    string Prompt,
    string KnowledgeScopeType,
    IReadOnlyList<Guid>? SelectedFileIds,
    string GenerationDeployment,
    double Temperature,
    int MaximumOutputTokens,
    double? TopP,
    string RetrievalMode,
    int RetrievalCount,
    double MinimumSimilarity,
    int MaximumEvidenceTokens,
    bool IncludeNeighbors,
    bool StrictGrounding,
    bool CitationsRequired);

/// <summary>
/// TASK-AGT-02: response shape for every agent read/write endpoint.
/// <see cref="Prompt"/> is <see langword="null"/> when redacted for the
/// caller (FR-AGT-006 - Viewers never receive the full prompt, only
/// SuperAdmin/ApplicationAdmin do); every other field is always populated.
/// </summary>
public sealed record AgentResponse(
    Guid Id,
    Guid ApplicationId,
    string Name,
    string? Description,
    string? Prompt,
    string Status,
    string KnowledgeScopeType,
    IReadOnlyList<Guid> SelectedFileIds,
    string GenerationDeployment,
    double Temperature,
    int MaximumOutputTokens,
    double? TopP,
    string RetrievalMode,
    int RetrievalCount,
    double MinimumSimilarity,
    int MaximumEvidenceTokens,
    bool IncludeNeighbors,
    bool StrictGrounding,
    bool CitationsRequired,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    string? UpdatedBy,
    DateTimeOffset? UpdatedAt,
    uint ConcurrencyToken);
