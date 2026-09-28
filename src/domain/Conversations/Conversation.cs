using System.Text.Json;
using EnterpriseKnowledgeAgent.Shared.Agents;
using EnterpriseKnowledgeAgent.Shared.Retrieval;

namespace EnterpriseKnowledgeAgent.Domain.Conversations;

/// <summary>
/// The <c>Conversation</c> aggregate (Module CHAT): a knowledge-base chat
/// thread owning an ordered list of <see cref="ConversationMessage"/> turns.
/// A conversation snapshots the retrieval/generation settings it was created
/// with so answers are reproducible and independent of any later agent edits.
/// Conversations are shared across the application (any member who can view
/// the application sees them); only the creator or an application admin may
/// rename or delete one (enforced in the API layer). All state changes go
/// through guarded methods here, mirroring the <see cref="Agents.Agent"/>
/// aggregate convention.
/// </summary>
public sealed class Conversation
{
    /// <summary>Default title shown until the first user message supplies one.</summary>
    public const string DefaultTitle = "New chat";

    /// <summary>Maximum length enforced for <see cref="Title"/> (mirrors the EF configuration's column length).</summary>
    public const int MaxTitleLength = 200;

    /// <summary>How many characters of the first user message are used to auto-name a conversation.</summary>
    public const int TitleSourceLength = 60;

    /// <summary>Generous cap for an optional custom system prompt supplied via advanced configuration.</summary>
    public const int MaxSystemPromptLength = 100_000;

    /// <summary>Bounds enforced on retrieval settings (shared with <see cref="Agents.Agent"/>).</summary>
    public const int MinRetrievalCount = 1;
    public const int MaxRetrievalCount = 50;
    public const double MinMinimumSimilarity = 0.0;
    public const double MaxMinimumSimilarity = 1.0;
    public const double MinTemperature = 0.0;
    public const double MaxTemperature = 2.0;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly List<ConversationMessage> _messages = [];

    public Guid Id { get; private set; }

    public Guid ApplicationId { get; private set; }

    public string Title { get; private set; } = DefaultTitle;

    /// <summary>Keycloak subject (<c>sub</c>) of the caller who started this conversation (opaque string, not a foreign key).</summary>
    public string CreatedBy { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    /// <summary>Timestamp of the most recent message activity - drives the conversation list ordering.</summary>
    public DateTimeOffset LastMessageAt { get; private set; }

    /// <summary>Soft-deleted conversations are hidden from all listings and reject further messages.</summary>
    public bool IsDeleted { get; private set; }

    // --- Settings snapshot (captured at creation, used for every turn) ---

    public KnowledgeScopeType KnowledgeScopeType { get; private set; }

    /// <summary>Raw JSON array of file ids - only populated when <see cref="KnowledgeScopeType"/> is <see cref="KnowledgeScopeType.Selected"/>. Use <see cref="SelectedFileIds"/> to read.</summary>
    public string? SelectedFileIdsJson { get; private set; }

    public string GenerationDeployment { get; private set; } = string.Empty;

    public double Temperature { get; private set; }

    public RetrievalMode RetrievalMode { get; private set; }

    public int RetrievalCount { get; private set; }

    public double MinimumSimilarity { get; private set; }

    public int MaximumEvidenceTokens { get; private set; }

    public bool IncludeNeighbors { get; private set; }

    public bool CitationsRequired { get; private set; }

    /// <summary>Optional custom system persona; <see langword="null"/> uses the platform's default knowledge-assistant persona.</summary>
    public string? SystemPrompt { get; private set; }

    /// <summary>Postgres' <c>xmin</c> concurrency token (see <c>ConversationConfiguration</c>).</summary>
    public uint ConcurrencyToken { get; private set; }

    public IReadOnlyList<ConversationMessage> Messages => _messages;

    /// <summary>Parses <see cref="SelectedFileIdsJson"/>; empty when the scope is <see cref="KnowledgeScopeType.AllReadyEnabled"/>.</summary>
    public IReadOnlyList<Guid> SelectedFileIds =>
        SelectedFileIdsJson is null
            ? []
            : JsonSerializer.Deserialize<List<Guid>>(SelectedFileIdsJson, JsonOptions) ?? [];

#pragma warning disable CS8618 // required by EF Core materialization; all properties are always populated via Create/mapping.
    private Conversation()
    {
    }
#pragma warning restore CS8618

    public static Conversation Create(
        Guid applicationId,
        string createdBy,
        KnowledgeScopeType knowledgeScopeType,
        IReadOnlyList<Guid>? selectedFileIds,
        string generationDeployment,
        double temperature,
        RetrievalMode retrievalMode,
        int retrievalCount,
        double minimumSimilarity,
        int maximumEvidenceTokens,
        bool includeNeighbors,
        bool citationsRequired,
        string? systemPrompt,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);
        ArgumentException.ThrowIfNullOrWhiteSpace(generationDeployment);

        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            Title = DefaultTitle,
            CreatedBy = createdBy,
            CreatedAt = now,
            LastMessageAt = now,
            GenerationDeployment = generationDeployment.Trim(),
            Temperature = ValidateRange(temperature, MinTemperature, MaxTemperature, nameof(temperature)),
            RetrievalMode = retrievalMode,
            RetrievalCount = ValidateRange(retrievalCount, MinRetrievalCount, MaxRetrievalCount, nameof(retrievalCount)),
            MinimumSimilarity = ValidateRange(minimumSimilarity, MinMinimumSimilarity, MaxMinimumSimilarity, nameof(minimumSimilarity)),
            MaximumEvidenceTokens = maximumEvidenceTokens > 0
                ? maximumEvidenceTokens
                : throw new ArgumentException("Maximum evidence tokens must be positive.", nameof(maximumEvidenceTokens)),
            IncludeNeighbors = includeNeighbors,
            CitationsRequired = citationsRequired,
            SystemPrompt = NormalizeSystemPrompt(systemPrompt),
        };

        conversation.SetKnowledgeScope(knowledgeScopeType, selectedFileIds);
        return conversation;
    }

    /// <summary>Appends a user question, auto-naming the conversation from the first question, and returns the created message.</summary>
    public ConversationMessage StartUserMessage(string content, DateTimeOffset now)
    {
        ThrowIfDeleted();
        var message = ConversationMessage.CreateUser(Id, content, _messages.Count, now);
        _messages.Add(message);

        if (Title == DefaultTitle)
        {
            Title = DeriveTitle(message.Content);
        }

        LastMessageAt = now;
        UpdatedAt = now;
        return message;
    }

    /// <summary>Appends an empty streaming assistant placeholder and returns it for the pipeline to finalize.</summary>
    public ConversationMessage StartAssistantMessage(DateTimeOffset now)
    {
        ThrowIfDeleted();
        var message = ConversationMessage.CreateAssistant(Id, _messages.Count, now);
        _messages.Add(message);
        return message;
    }

    /// <summary>Records that the latest turn finished, refreshing the list-ordering timestamp.</summary>
    public void TouchAfterAnswer(DateTimeOffset now)
    {
        LastMessageAt = now;
        UpdatedAt = now;
    }

    public void Rename(string title, DateTimeOffset now)
    {
        ThrowIfDeleted();
        Title = NormalizeTitle(title);
        UpdatedAt = now;
    }

    /// <summary>Idempotent: soft-deleting an already-deleted conversation is a no-op.</summary>
    public void SoftDelete(DateTimeOffset now)
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
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
        if (IsDeleted)
        {
            throw new ConversationStateConflictException($"Conversation '{Id}' is deleted and can no longer be used.");
        }
    }

    private static string DeriveTitle(string firstUserMessage)
    {
        var collapsed = string.Join(' ', firstUserMessage.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (collapsed.Length <= TitleSourceLength)
        {
            return collapsed.Length == 0 ? DefaultTitle : collapsed;
        }

        return string.Concat(collapsed.AsSpan(0, TitleSourceLength).TrimEnd(), "…");
    }

    private static string NormalizeTitle(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        var trimmed = title.Trim();
        if (trimmed.Length > MaxTitleLength)
        {
            throw new ArgumentException($"Title must be at most {MaxTitleLength} characters.", nameof(title));
        }

        return trimmed;
    }

    private static string? NormalizeSystemPrompt(string? systemPrompt)
    {
        if (string.IsNullOrWhiteSpace(systemPrompt))
        {
            return null;
        }

        var trimmed = systemPrompt.Trim();
        if (trimmed.Length > MaxSystemPromptLength)
        {
            throw new ArgumentException(
                $"System prompt must be at most {MaxSystemPromptLength} characters.", nameof(systemPrompt));
        }

        return trimmed;
    }

    private static double ValidateRange(double value, double min, double max, string paramName) =>
        value < min || value > max
            ? throw new ArgumentException($"{paramName} must be between {min} and {max}.", paramName)
            : value;

    private static int ValidateRange(int value, int min, int max, string paramName) =>
        value < min || value > max
            ? throw new ArgumentException($"{paramName} must be between {min} and {max}.", paramName)
            : value;
}
