namespace EnterpriseKnowledgeAgent.Application.Chat;

// -------------------------------------------------------------------------
// Requests
// -------------------------------------------------------------------------

/// <summary>
/// Module CHAT: optional advanced configuration supplied when starting a
/// conversation. Every field is nullable - omitting a field (or the whole
/// object) uses the platform default (<see cref="ChatDefaults"/>), keeping the
/// common case config-free.
/// </summary>
/// <param name="KnowledgeScope">"AllReadyEnabled" (default) or "Selected".</param>
/// <param name="SelectedFileIds">Required (non-empty) only when <paramref name="KnowledgeScope"/> is "Selected".</param>
/// <param name="GenerationDeployment">A specific chat deployment name, or null for the platform default.</param>
/// <param name="Temperature">Sampling temperature override, or null for the default.</param>
/// <param name="RetrievalMode">"Semantic" (default) or "Hybrid".</param>
/// <param name="SystemPrompt">A custom assistant persona, or null for the default persona.</param>
public sealed record ChatAdvancedConfigRequest(
    string? KnowledgeScope = null,
    IReadOnlyList<Guid>? SelectedFileIds = null,
    string? GenerationDeployment = null,
    double? Temperature = null,
    string? RetrievalMode = null,
    string? SystemPrompt = null);

/// <summary>Module CHAT: starts a new conversation, optionally with advanced configuration.</summary>
public sealed record CreateConversationRequest(ChatAdvancedConfigRequest? Advanced = null);

/// <summary>Module CHAT: renames a conversation.</summary>
public sealed record RenameConversationRequest(string Title);

/// <summary>Module CHAT: sends a user question into a conversation (the answer streams back over SSE).</summary>
public sealed record SendMessageRequest(string Content);

// -------------------------------------------------------------------------
// Responses
// -------------------------------------------------------------------------

/// <summary>Module CHAT: a lightweight conversation entry for the sidebar list.</summary>
public sealed record ConversationSummaryResponse(
    Guid Id,
    string Title,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastMessageAt,
    int MessageCount);

/// <summary>Module CHAT: the immutable settings snapshot a conversation runs with.</summary>
public sealed record ConversationSettingsResponse(
    string KnowledgeScope,
    IReadOnlyList<Guid> SelectedFileIds,
    string GenerationDeployment,
    double Temperature,
    string RetrievalMode,
    bool HasCustomSystemPrompt);

/// <summary>Module CHAT: one retrieved source snapshot backing an assistant answer's citation (shaped to match the output citation-drawer contract).</summary>
public sealed record ChatEvidenceResponse(
    string SourceId,
    Guid FileId,
    Guid ChunkId,
    string FileName,
    string HeadingPath,
    string Passage,
    int CharacterStart,
    int CharacterEnd,
    bool SourceUnavailable);

/// <summary>Module CHAT: a single message in a conversation.</summary>
public sealed record ConversationMessageResponse(
    Guid Id,
    string Role,
    string Content,
    string Status,
    int Sequence,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? FailureCategory,
    string? FailureMessage,
    IReadOnlyList<string> CitedSourceIds,
    IReadOnlyList<ChatEvidenceResponse> Evidence);

/// <summary>Module CHAT: a full conversation with its message history.</summary>
public sealed record ConversationDetailResponse(
    Guid Id,
    string Title,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastMessageAt,
    ConversationSettingsResponse Settings,
    IReadOnlyList<ConversationMessageResponse> Messages);

// -------------------------------------------------------------------------
// Server-sent streaming events (emitted by ChatCompletionService)
// -------------------------------------------------------------------------

/// <summary>Module CHAT: base type for a single server-sent event during answer streaming.</summary>
public abstract record ChatStreamEvent;

/// <summary>A fragment of the assistant's answer as it is generated.</summary>
public sealed record ChatTokenEvent(string Text) : ChatStreamEvent;

/// <summary>The terminal success/no-results event, carrying the persisted message id, final status, and citations.</summary>
public sealed record ChatDoneEvent(
    Guid MessageId,
    string Status,
    IReadOnlyList<string> CitedSourceIds,
    IReadOnlyList<ChatEvidenceResponse> Evidence) : ChatStreamEvent;

/// <summary>A terminal failure event (retrieval/generation error), with a category and user-safe message.</summary>
public sealed record ChatErrorEvent(
    Guid MessageId,
    string Category,
    string Message) : ChatStreamEvent;
