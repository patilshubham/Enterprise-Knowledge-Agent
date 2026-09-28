namespace EnterpriseKnowledgeAgent.Application.AzureOpenAI;

/// <summary>
/// TASK-AOI-03: Azure OpenAI embedding adapter used by module 13 (TASK-EMB-01).
/// Implementations must apply bounded exponential retry (honoring any
/// server-supplied Retry-After) and an explicit request timeout, and must
/// translate failures into <see cref="AzureOpenAiException"/> with the
/// correct <see cref="EnterpriseKnowledgeAgent.Shared.Errors.ErrorCategory"/>.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>Generates one embedding vector per input, in the same order as <paramref name="inputs"/>.</summary>
    Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> inputs, string deployment, CancellationToken cancellationToken = default);

    /// <summary>TASK-AOI-05: a lightweight reachability probe - does not throw, returns false on any failure.</summary>
    Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);
}
