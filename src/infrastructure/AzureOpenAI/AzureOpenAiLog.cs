using Microsoft.Extensions.Logging;

namespace EnterpriseKnowledgeAgent.Infrastructure.AzureOpenAI;

/// <summary>Source-generated log messages (CA1848) for <see cref="AzureOpenAiTextGenerationService"/>.</summary>
internal static partial class AzureOpenAiLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Azure OpenAI chat deployment {Deployment} health check failed.")]
    public static partial void ChatHealthCheckFailed(this ILogger logger, Exception exception, string deployment);
}
