namespace EnterpriseKnowledgeAgent.Api;

/// <summary>
/// Source-generated log messages for the TASK-OUT-03/04 output lifecycle
/// audit hooks (mirrors the TASK-APP-02 pattern in <see cref="ApplicationAuditLog"/>).
/// TASK-AUD-01 formalizes these into the durable audit schema/store; until
/// then this is the auditable trail the endpoints emit.
/// </summary>
internal static partial class OutputAuditLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "output.edited applicationId={ApplicationId} outputId={OutputId} actor={Actor}")]
    public static partial void OutputEdited(this ILogger logger, Guid applicationId, Guid outputId, string actor);

    [LoggerMessage(Level = LogLevel.Information, Message = "output.regenerated applicationId={ApplicationId} outputId={OutputId} newRunId={NewRunId} actor={Actor}")]
    public static partial void OutputRegenerated(this ILogger logger, Guid applicationId, Guid outputId, Guid newRunId, string actor);

    [LoggerMessage(Level = LogLevel.Information, Message = "output.deleted applicationId={ApplicationId} outputId={OutputId} actor={Actor}")]
    public static partial void OutputDeleted(this ILogger logger, Guid applicationId, Guid outputId, string actor);
}
