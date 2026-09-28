namespace EnterpriseKnowledgeAgent.Api;

/// <summary>
/// Source-generated log messages for the TASK-APP-02 application lifecycle
/// audit hooks (mirrors the TASK-IDN-04 pattern in <see cref="AuthAuditLog"/>).
/// TASK-AUD-01 formalizes these into the durable audit schema/store; until
/// then this is the auditable trail the endpoints emit.
/// </summary>
internal static partial class ApplicationAuditLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "application.created applicationId={ApplicationId} name={Name} actor={Actor}")]
    public static partial void Created(this ILogger logger, Guid applicationId, string name, string actor);

    [LoggerMessage(Level = LogLevel.Information, Message = "application.updated applicationId={ApplicationId} actor={Actor}")]
    public static partial void Updated(this ILogger logger, Guid applicationId, string actor);

    [LoggerMessage(Level = LogLevel.Information, Message = "application.archived applicationId={ApplicationId} actor={Actor}")]
    public static partial void Archived(this ILogger logger, Guid applicationId, string actor);

    [LoggerMessage(Level = LogLevel.Information, Message = "application.restored applicationId={ApplicationId} actor={Actor}")]
    public static partial void Restored(this ILogger logger, Guid applicationId, string actor);

    [LoggerMessage(Level = LogLevel.Information, Message = "application.deleted applicationId={ApplicationId} actor={Actor}")]
    public static partial void Deleted(this ILogger logger, Guid applicationId, string actor);

    [LoggerMessage(Level = LogLevel.Information, Message = "application.viewer_execution_changed applicationId={ApplicationId} allowed={Allowed} actor={Actor}")]
    public static partial void ViewerExecutionChanged(this ILogger logger, Guid applicationId, bool allowed, string actor);
}
