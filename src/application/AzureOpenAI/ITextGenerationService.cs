namespace EnterpriseKnowledgeAgent.Application.AzureOpenAI;

/// <summary>A single chat message for a text-generation request.</summary>
/// <param name="Role">"system", "user", or "assistant".</param>
public sealed record ChatMessage(string Role, string Content);

/// <summary>
/// TASK-GEN-01: per-call generation parameters. <paramref name="TopP"/> should
/// already be <see langword="null"/> when the caller (module 20/21) has
/// determined the target deployment does not support it
/// (<see cref="DeploymentInfo.SupportsTopP"/>) - this type does not itself
/// re-check capability, it just carries whatever the caller decided to send.
/// </summary>
/// <param name="RequestStructuredOutput">TASK-GEN-04: when <see langword="true"/>, the implementation requests a schema-constrained ("structured output") completion whose content is a JSON object of the shape <c>{ "markdown": "..." }</c>, instead of a plain-text completion. Callers must only set this when the target deployment is known to support it (<see cref="DeploymentInfo.SupportsStructuredOutput"/>) - this type does not itself re-check capability.</param>
public sealed record GenerationParameters(double Temperature, int MaximumOutputTokens, double? TopP, bool RequestStructuredOutput = false);

/// <summary>Result of a text-generation call.</summary>
/// <param name="Content">The generated text.</param>
/// <param name="FinishReason">Provider-reported finish reason (e.g. "stop", "length", "content_filter").</param>
public sealed record TextGenerationResult(string Content, string? FinishReason);

/// <summary>
/// TASK-AOI-03: Azure OpenAI chat/completions adapter used by later
/// generation modules (module 20+). Implementations must apply bounded
/// exponential retry (honoring any server-supplied Retry-After) and an
/// explicit request timeout, and must translate failures into
/// <see cref="AzureOpenAiException"/> with the correct
/// <see cref="EnterpriseKnowledgeAgent.Shared.Errors.ErrorCategory"/>
/// (including <c>ContextOverflow</c> when the provider reports the prompt
/// exceeded the model's context window).
/// </summary>
public interface ITextGenerationService
{
    /// <param name="parameters">TASK-GEN-01: when <see langword="null"/>, the deployment's own defaults apply (used by callers that don't yet have agent-configured parameters, e.g. health probes).</param>
    Task<TextGenerationResult> GenerateAsync(IReadOnlyList<ChatMessage> messages, string deployment, GenerationParameters? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Module CHAT: streams the generated answer as a sequence of text
    /// fragments (tokens) for the real-time chat experience. Honors the same
    /// error-mapping contract as <see cref="GenerateAsync"/> (failures surface
    /// as <see cref="AzureOpenAiException"/>) and stops promptly when
    /// <paramref name="cancellationToken"/> is cancelled (e.g. the client
    /// disconnects). Unlike <see cref="GenerateAsync"/> it does not retry
    /// mid-stream - a transient failure surfaces to the caller once emission
    /// has begun.
    /// </summary>
    IAsyncEnumerable<string> GenerateStreamingAsync(IReadOnlyList<ChatMessage> messages, string deployment, GenerationParameters? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>TASK-AOI-05: a lightweight reachability probe - does not throw, returns false on any failure.</summary>
    Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);
}
