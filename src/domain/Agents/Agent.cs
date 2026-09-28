using System.Text.Json;
using EnterpriseKnowledgeAgent.Shared.Agents;
using EnterpriseKnowledgeAgent.Shared.Retrieval;

namespace EnterpriseKnowledgeAgent.Domain.Agents;

/// <summary>
/// TASK-AGT-01: the <c>Agent</c> aggregate (FR-AGT-*, FR-PAR-*, ASDD §9.3).
/// One current editable configuration per agent - there is no version
/// history screen (FR-AGT-005); every agent run instead snapshots the
/// fields it used (<c>AgentRun</c>, module 19). All state changes go
/// through guarded methods here rather than public setters, mirroring
/// <see cref="Applications.Application"/>'s convention.
/// </summary>
public sealed class Agent
{
    /// <summary>Maximum length enforced for <see cref="Name"/> (mirrors the EF configuration's column length).</summary>
    public const int MaxNameLength = 200;

    /// <summary>Maximum length enforced for <see cref="Description"/> (mirrors the EF configuration's column length).</summary>
    public const int MaxDescriptionLength = 2000;

    /// <summary>TASK-AGT-04/FR-AGT-003: generous cap so large prompts are supported without silent truncation, while still bounding the database column.</summary>
    public const int MaxPromptLength = 100_000;

    /// <summary>FR-PAR-001: Azure OpenAI's supported temperature range for chat completions.</summary>
    public const double MinTemperature = 0.0;
    public const double MaxTemperature = 2.0;

    /// <summary>FR-PAR-001: valid range for <c>top_p</c> when supplied.</summary>
    public const double MinTopP = 0.0;
    public const double MaxTopP = 1.0;

    /// <summary>FR-PAR-002: bounds enforced on retrieval parameters independent of any deployment capability (deployment-specific bounds are enforced by <c>AgentParameterValidator</c>, TASK-AGT-03).</summary>
    public const int MinRetrievalCount = 1;
    public const int MaxRetrievalCount = 50;
    public const double MinMinimumSimilarity = 0.0;
    public const double MaxMinimumSimilarity = 1.0;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Guid Id { get; private set; }

    public Guid ApplicationId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string Prompt { get; private set; } = string.Empty;

    public AgentStatus Status { get; private set; }

    public KnowledgeScopeType KnowledgeScopeType { get; private set; }

    /// <summary>Raw JSON array of file ids - only populated when <see cref="KnowledgeScopeType"/> is <see cref="KnowledgeScopeType.Selected"/>. Use <see cref="SelectedFileIds"/> to read.</summary>
    public string? SelectedFileIdsJson { get; private set; }

    public string GenerationDeployment { get; private set; } = string.Empty;

    public double Temperature { get; private set; }

    public int MaximumOutputTokens { get; private set; }

    /// <summary><see langword="null"/> when not set, or when TASK-AGT-03's capability validation cleared it for a deployment that does not support it.</summary>
    public double? TopP { get; private set; }

    public RetrievalMode RetrievalMode { get; private set; }

    public int RetrievalCount { get; private set; }

    public double MinimumSimilarity { get; private set; }

    public int MaximumEvidenceTokens { get; private set; }

    public bool IncludeNeighbors { get; private set; }

    public bool StrictGrounding { get; private set; }

    public bool CitationsRequired { get; private set; }

    /// <summary>Keycloak subject (<c>sub</c>) of the caller who created this agent (decision D20 - opaque string, not a foreign key).</summary>
    public string CreatedBy { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public string? UpdatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    /// <summary>ASDD §9.3's <c>ConcurrencyToken</c>, mapped to Postgres' <c>xmin</c> system column (see <c>AgentConfiguration</c>) rather than a hand-rolled column - EF bumps it automatically on every update and uses it to detect lost updates.</summary>
    public uint ConcurrencyToken { get; private set; }

    /// <summary>Deleted agents reject all further state-changing operations (a terminal state, mirroring <see cref="Applications.Application"/>).</summary>
    public bool IsReadOnly => Status == AgentStatus.Deleted;

    /// <summary>Parses <see cref="SelectedFileIdsJson"/>; empty when the scope is <see cref="KnowledgeScopeType.AllReadyEnabled"/>.</summary>
    public IReadOnlyList<Guid> SelectedFileIds =>
        SelectedFileIdsJson is null
            ? []
            : JsonSerializer.Deserialize<List<Guid>>(SelectedFileIdsJson, JsonOptions) ?? [];

#pragma warning disable CS8618 // required by EF Core materialization; all properties are always populated via Create/mapping.
    private Agent()
    {
    }
#pragma warning restore CS8618

    public static Agent Create(
        Guid applicationId,
        string name,
        string? description,
        string prompt,
        KnowledgeScopeType knowledgeScopeType,
        IReadOnlyList<Guid>? selectedFileIds,
        string generationDeployment,
        double temperature,
        int maximumOutputTokens,
        double? topP,
        RetrievalMode retrievalMode,
        int retrievalCount,
        double minimumSimilarity,
        int maximumEvidenceTokens,
        bool includeNeighbors,
        bool strictGrounding,
        bool citationsRequired,
        string createdBy,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);

        var agent = new Agent
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            Status = AgentStatus.Active,
            CreatedBy = createdBy,
            CreatedAt = now,
        };

        agent.Update(
            name, description, prompt, knowledgeScopeType, selectedFileIds, generationDeployment, temperature,
            maximumOutputTokens, topP, retrievalMode, retrievalCount, minimumSimilarity, maximumEvidenceTokens,
            includeNeighbors, strictGrounding, citationsRequired, createdBy, now);

        // Update() sets UpdatedBy/UpdatedAt as a side effect - creation itself hasn't been "updated" yet.
        agent.UpdatedBy = null;
        agent.UpdatedAt = null;

        return agent;
    }

    /// <summary>TASK-AGT-02/FR-AGT-005: overwrites the single current configuration wholesale - there is no partial-patch or version-history concept.</summary>
    public void Update(
        string name,
        string? description,
        string prompt,
        KnowledgeScopeType knowledgeScopeType,
        IReadOnlyList<Guid>? selectedFileIds,
        string generationDeployment,
        double temperature,
        int maximumOutputTokens,
        double? topP,
        RetrievalMode retrievalMode,
        int retrievalCount,
        double minimumSimilarity,
        int maximumEvidenceTokens,
        bool includeNeighbors,
        bool strictGrounding,
        bool citationsRequired,
        string updatedBy,
        DateTimeOffset now)
    {
        ThrowIfDeleted();
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);
        ArgumentException.ThrowIfNullOrWhiteSpace(generationDeployment);

        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        Prompt = NormalizePrompt(prompt);
        SetKnowledgeScope(knowledgeScopeType, selectedFileIds);
        GenerationDeployment = generationDeployment.Trim();
        Temperature = ValidateRange(temperature, MinTemperature, MaxTemperature, nameof(temperature));
        MaximumOutputTokens = maximumOutputTokens > 0
            ? maximumOutputTokens
            : throw new ArgumentException("Maximum output tokens must be positive.", nameof(maximumOutputTokens));
        TopP = topP is { } value ? ValidateRange(value, MinTopP, MaxTopP, nameof(topP)) : null;
        RetrievalMode = retrievalMode;
        RetrievalCount = ValidateRange(retrievalCount, MinRetrievalCount, MaxRetrievalCount, nameof(retrievalCount));
        MinimumSimilarity = ValidateRange(minimumSimilarity, MinMinimumSimilarity, MaxMinimumSimilarity, nameof(minimumSimilarity));
        MaximumEvidenceTokens = maximumEvidenceTokens > 0
            ? maximumEvidenceTokens
            : throw new ArgumentException("Maximum evidence tokens must be positive.", nameof(maximumEvidenceTokens));
        IncludeNeighbors = includeNeighbors;
        StrictGrounding = strictGrounding;
        CitationsRequired = citationsRequired;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void Enable(string updatedBy, DateTimeOffset now)
    {
        ThrowIfDeleted();
        if (Status != AgentStatus.Disabled)
        {
            throw new AgentStateConflictException($"Agent '{Id}' must be Disabled to enable (current status: {Status}).");
        }

        Status = AgentStatus.Active;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void Disable(string updatedBy, DateTimeOffset now)
    {
        ThrowIfDeleted();
        if (Status != AgentStatus.Active)
        {
            throw new AgentStateConflictException($"Agent '{Id}' must be Active to disable (current status: {Status}).");
        }

        Status = AgentStatus.Disabled;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    /// <summary>Idempotent (mirrors <see cref="Applications.Application.Delete"/>): deleting an already-deleted agent is a no-op, not an error.</summary>
    public void Delete(string updatedBy, DateTimeOffset now)
    {
        if (Status == AgentStatus.Deleted)
        {
            return;
        }

        Status = AgentStatus.Deleted;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    private void SetKnowledgeScope(KnowledgeScopeType scopeType, IReadOnlyList<Guid>? selectedFileIds)
    {
        if (scopeType == KnowledgeScopeType.Selected)
        {
            if (selectedFileIds is null || selectedFileIds.Count == 0)
            {
                throw new ArgumentException(
                    "At least one file must be selected when the knowledge scope is Selected.", nameof(selectedFileIds));
            }

            KnowledgeScopeType = KnowledgeScopeType.Selected;
            SelectedFileIdsJson = JsonSerializer.Serialize(selectedFileIds.Distinct().ToList(), JsonOptions);
        }
        else
        {
            KnowledgeScopeType = KnowledgeScopeType.AllReadyEnabled;
            SelectedFileIdsJson = null;
        }
    }

    private void ThrowIfDeleted()
    {
        if (Status == AgentStatus.Deleted)
        {
            throw new AgentStateConflictException($"Agent '{Id}' is deleted and can no longer be edited.");
        }
    }

    private static double ValidateRange(double value, double min, double max, string paramName) =>
        value < min || value > max
            ? throw new ArgumentException($"{paramName} must be between {min} and {max}.", paramName)
            : value;

    private static int ValidateRange(int value, int min, int max, string paramName) =>
        value < min || value > max
            ? throw new ArgumentException($"{paramName} must be between {min} and {max}.", paramName)
            : value;

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

    private static string NormalizePrompt(string prompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        if (prompt.Length > MaxPromptLength)
        {
            throw new ArgumentException(
                $"Prompt must be at most {MaxPromptLength} characters.", nameof(prompt));
        }

        // FR-AGT-003: the prompt is never trimmed/truncated beyond whitespace normalization at the edges.
        return prompt.Trim();
    }
}
