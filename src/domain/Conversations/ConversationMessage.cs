using System.Text.Json;
using EnterpriseKnowledgeAgent.Shared.Chat;
using EnterpriseKnowledgeAgent.Shared.Errors;

namespace EnterpriseKnowledgeAgent.Domain.Conversations;

/// <summary>
/// A single message in a <see cref="Conversation"/> (Module CHAT) - either a
/// user question (<see cref="MessageRole.User"/>, always
/// <see cref="MessageStatus.Completed"/>) or a knowledge-base-grounded
/// assistant answer (<see cref="MessageRole.Assistant"/>) that starts
/// <see cref="MessageStatus.Streaming"/> and is finalized to a terminal state
/// as the runtime pipeline resolves. All state changes go through guarded
/// methods rather than public setters, mirroring the aggregate conventions in
/// <c>Agent</c>/<c>Application</c>.
/// </summary>
public sealed class ConversationMessage
{
    /// <summary>Maximum length accepted for a single user question (the UI enforces a lower limit for usability).</summary>
    public const int MaxUserContentLength = 8_000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Guid Id { get; private set; }

    public Guid ConversationId { get; private set; }

    public MessageRole Role { get; private set; }

    /// <summary>User question text, or the assistant's generated Markdown (accumulated as it streams).</summary>
    public string Content { get; private set; } = string.Empty;

    public MessageStatus Status { get; private set; }

    /// <summary>Zero-based position of this message within its conversation.</summary>
    public int Sequence { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When an assistant message reached a terminal state; <see langword="null"/> while streaming or for user messages.</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Raw JSON array of the <c>SOURCE-NNN</c> ids the answer actually cited (validated). Use <see cref="CitedSourceIds"/> to read.</summary>
    public string? CitedSourceIdsJson { get; private set; }

    public ErrorCategory? FailureCategory { get; private set; }

    public string? FailureMessage { get; private set; }

    /// <summary>Parses <see cref="CitedSourceIdsJson"/>; empty when nothing was cited.</summary>
    public IReadOnlyList<string> CitedSourceIds =>
        CitedSourceIdsJson is null
            ? []
            : JsonSerializer.Deserialize<List<string>>(CitedSourceIdsJson, JsonOptions) ?? [];

#pragma warning disable CS8618 // required by EF Core materialization; all properties are always populated via Create/mapping.
    private ConversationMessage()
    {
    }
#pragma warning restore CS8618

    /// <summary>Creates a completed user question.</summary>
    public static ConversationMessage CreateUser(Guid conversationId, string content, int sequence, DateTimeOffset now)
    {
        var normalized = NormalizeUserContent(content);
        return new ConversationMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = MessageRole.User,
            Content = normalized,
            Status = MessageStatus.Completed,
            Sequence = sequence,
            CreatedAt = now,
            CompletedAt = now,
        };
    }

    /// <summary>Creates an empty assistant placeholder in <see cref="MessageStatus.Streaming"/>, to be finalized once generation resolves.</summary>
    public static ConversationMessage CreateAssistant(Guid conversationId, int sequence, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        ConversationId = conversationId,
        Role = MessageRole.Assistant,
        Content = string.Empty,
        Status = MessageStatus.Streaming,
        Sequence = sequence,
        CreatedAt = now,
    };

    /// <summary>Finalizes a streaming assistant message with its generated Markdown and the validated cited source ids.</summary>
    public void Complete(string content, IReadOnlyList<string> citedSourceIds, DateTimeOffset now)
    {
        EnsureStreamingAssistant();
        ArgumentNullException.ThrowIfNull(citedSourceIds);
        Content = content ?? string.Empty;
        CitedSourceIdsJson = JsonSerializer.Serialize(citedSourceIds, JsonOptions);
        Status = MessageStatus.Completed;
        CompletedAt = now;
    }

    /// <summary>Finalizes a streaming assistant message that found no usable evidence in the knowledge base.</summary>
    public void SetNoResults(string content, DateTimeOffset now)
    {
        EnsureStreamingAssistant();
        Content = content ?? string.Empty;
        Status = MessageStatus.NoResults;
        CompletedAt = now;
    }

    /// <summary>Finalizes a streaming assistant message that failed to generate.</summary>
    public void Fail(ErrorCategory category, string message, DateTimeOffset now)
    {
        EnsureStreamingAssistant();
        Status = MessageStatus.Failed;
        FailureCategory = category;
        FailureMessage = message;
        CompletedAt = now;
    }

    /// <summary>Finalizes a streaming assistant message that was cancelled (client disconnected) mid-generation, keeping any partial content.</summary>
    public void Cancel(string partialContent, DateTimeOffset now)
    {
        EnsureStreamingAssistant();
        Content = partialContent ?? string.Empty;
        Status = MessageStatus.Cancelled;
        CompletedAt = now;
    }

    private void EnsureStreamingAssistant()
    {
        if (Role != MessageRole.Assistant)
        {
            throw new ConversationStateConflictException($"Message '{Id}' is not an assistant message.");
        }

        if (Status != MessageStatus.Streaming)
        {
            throw new ConversationStateConflictException(
                $"Assistant message '{Id}' must be Streaming to finalize (current status: {Status}).");
        }
    }

    private static string NormalizeUserContent(string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        var trimmed = content.Trim();
        if (trimmed.Length > MaxUserContentLength)
        {
            throw new ArgumentException(
                $"Message must be at most {MaxUserContentLength} characters.", nameof(content));
        }

        return trimmed;
    }
}
