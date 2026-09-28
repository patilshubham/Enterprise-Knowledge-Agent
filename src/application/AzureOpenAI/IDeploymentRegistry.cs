namespace EnterpriseKnowledgeAgent.Application.AzureOpenAI;

/// <summary>Non-secret capability read-model for one configured Azure OpenAI deployment (TASK-AOI-01).</summary>
/// <param name="Name">The deployment name as configured in Azure OpenAI (never the API key/endpoint).</param>
/// <param name="Purpose">What the deployment is used for.</param>
/// <param name="MaxContextTokens">Only populated for <see cref="DeploymentPurpose.Chat"/> deployments.</param>
/// <param name="MaxOutputTokens">Only populated for <see cref="DeploymentPurpose.Chat"/> deployments.</param>
/// <param name="EmbeddingDimensions">Only populated for <see cref="DeploymentPurpose.Embedding"/> deployments.</param>
/// <param name="SupportsTopP">TASK-AGT-03: whether this deployment accepts the <c>top_p</c> parameter. Only meaningful for <see cref="DeploymentPurpose.Chat"/> deployments; always <see langword="false"/> for <see cref="DeploymentPurpose.Embedding"/>.</param>
/// <param name="SupportsStructuredOutput">TASK-GEN-04: whether this deployment reliably supports schema-constrained ("structured output") chat completions. Reliability varies per deployment (D4), so this defaults conservatively to <see langword="false"/> - callers should still be prepared to fall back to constrained citation-marker parsing (TASK-CIT-03) regardless. Only meaningful for <see cref="DeploymentPurpose.Chat"/> deployments.</param>
/// <param name="SupportsTemperature">Whether this deployment accepts a non-default <c>temperature</c>. Reasoning-style deployments (e.g. gpt-5.5) reject any non-default temperature with a hard 400, so the UI hides the temperature control when this is <see langword="false"/> (the value is otherwise silently ignored server-side). Only meaningful for <see cref="DeploymentPurpose.Chat"/> deployments.</param>
public sealed record DeploymentInfo(
    string Name,
    DeploymentPurpose Purpose,
    int? MaxContextTokens,
    int? MaxOutputTokens,
    int? EmbeddingDimensions,
    bool SupportsTopP = false,
    bool SupportsStructuredOutput = false,
    bool SupportsTemperature = true);

/// <summary>What a configured Azure OpenAI deployment is used for.</summary>
public enum DeploymentPurpose
{
    Chat,
    Embedding,
}

/// <summary>TASK-AOI-01: read-only view of the currently configured Azure OpenAI deployments, exposed via <c>GET /api/deployments</c>.</summary>
public interface IDeploymentRegistry
{
    IReadOnlyList<DeploymentInfo> ListDeployments();
}
