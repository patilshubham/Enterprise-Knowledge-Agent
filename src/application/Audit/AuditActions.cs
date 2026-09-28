namespace EnterpriseKnowledgeAgent.Application.Audit;

/// <summary>
/// TASK-AUD-02: stable action codes for <see cref="IAuditWriter"/> callers,
/// covering the FR-17 audit list (auth, application lifecycle, knowledge
/// file lifecycle, agent lifecycle, agent execution, output edit/delete).
/// Kept as plain string constants (not an enum) so new actions can be added
/// by any module without a shared enum becoming a cross-module bottleneck -
/// mirrors the <c>RealtimeEventTypes</c> convention in <c>Shared/Realtime</c>.
/// </summary>
public static class AuditActions
{
    public const string ApplicationCreated = nameof(ApplicationCreated);
    public const string ApplicationUpdated = nameof(ApplicationUpdated);
    public const string ApplicationArchived = nameof(ApplicationArchived);
    public const string ApplicationRestored = nameof(ApplicationRestored);
    public const string ApplicationDeleted = nameof(ApplicationDeleted);
    public const string ApplicationViewerExecutionChanged = nameof(ApplicationViewerExecutionChanged);

    public const string KnowledgeFileUploaded = nameof(KnowledgeFileUploaded);
    public const string KnowledgeFileReplaced = nameof(KnowledgeFileReplaced);
    public const string KnowledgeFileDisabled = nameof(KnowledgeFileDisabled);
    public const string KnowledgeFileEnabled = nameof(KnowledgeFileEnabled);
    public const string KnowledgeFileDeleted = nameof(KnowledgeFileDeleted);
    public const string KnowledgeFileRetryRequested = nameof(KnowledgeFileRetryRequested);

    public const string AgentCreated = nameof(AgentCreated);
    public const string AgentUpdated = nameof(AgentUpdated);
    public const string AgentEnabled = nameof(AgentEnabled);
    public const string AgentDisabled = nameof(AgentDisabled);
    public const string AgentDeleted = nameof(AgentDeleted);

    public const string AgentRunSubmitted = nameof(AgentRunSubmitted);
    public const string AgentRunRetryRequested = nameof(AgentRunRetryRequested);
    public const string AgentRunCancelRequested = nameof(AgentRunCancelRequested);

    public const string OutputEdited = nameof(OutputEdited);
    public const string OutputRegenerated = nameof(OutputRegenerated);
    public const string OutputDeleted = nameof(OutputDeleted);

    public const string ConversationCreated = nameof(ConversationCreated);
    public const string ConversationRenamed = nameof(ConversationRenamed);
    public const string ConversationDeleted = nameof(ConversationDeleted);
    public const string ChatMessageSent = nameof(ChatMessageSent);
}
