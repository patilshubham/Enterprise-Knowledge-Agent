namespace EnterpriseKnowledgeAgent.Application.AzureOpenAI;

/// <summary>How the app authenticates to Azure OpenAI (TASK-AOI-02).</summary>
public enum AoiAuthMode
{
    /// <summary><see cref="Azure.Identity.DefaultAzureCredential"/> (managed identity/az login) - the recommended production mode.</summary>
    Entra,

    /// <summary>A server-side API key (never logged, never returned to any client). Convenient for local dev.</summary>
    ApiKey,
}

/// <summary>
/// TASK-AOI-01: strongly-typed Azure OpenAI configuration, bound from the
/// "AzureOpenAI" section (env var names per .env.example: <c>AZURE_OPENAI_*</c>).
/// Validated fail-fast at startup by <see cref="AoiOptionsValidator"/>.
/// </summary>
public sealed class AoiOptions
{
    public const string SectionName = "AzureOpenAI";

    /// <summary>Azure OpenAI resource endpoint, e.g. <c>https://my-resource.openai.azure.com/</c>.</summary>
    public string Endpoint { get; init; } = string.Empty;

    public AoiAuthMode AuthMode { get; init; } = AoiAuthMode.Entra;

    /// <summary>Required only when <see cref="AuthMode"/> is <see cref="AoiAuthMode.ApiKey"/>. Never logged or exposed via any API response.</summary>
    public string? ApiKey { get; init; }

    /// <summary>Chat/completions deployment name used for generation (module 20+).</summary>
    public string ChatDeployment { get; init; } = string.Empty;

    /// <summary>Embedding deployment name used by module 13 (TASK-EMB-01).</summary>
    public string EmbeddingDeployment { get; init; } = string.Empty;

    /// <summary>Output dimension of <see cref="EmbeddingDeployment"/> - must match the pgvector column width (TASK-PG-02, D10).</summary>
    public int EmbeddingDimensions { get; init; } = 1536;

    public int MaxContextTokens { get; init; } = 128_000;

    public int MaxOutputTokens { get; init; } = 4_096;

    /// <summary>TASK-AOI-01/AGT-03: whether <see cref="ChatDeployment"/> supports the <c>top_p</c> sampling parameter. Some model families (e.g. reasoning models) reject it; default <see langword="true"/> matches the common GPT-4o-class chat deployments this platform targets.</summary>
    public bool ChatDeploymentSupportsTopP { get; init; } = true;

    /// <summary>Whether <see cref="ChatDeployment"/> accepts a custom <c>temperature</c>. Reasoning-style model families (e.g. gpt-5.5) only accept the default value (1) and reject any other value with a hard 400; default <see langword="true"/> matches the common GPT-4o-class chat deployments this platform targets.</summary>
    public bool ChatDeploymentSupportsTemperature { get; init; } = true;

    /// <summary>Whether <see cref="ChatDeployment"/> accepts the legacy <c>max_tokens</c> request field. Reasoning-style model families (e.g. gpt-5.5) reject it outright and require the newer <c>max_completion_tokens</c> field instead; default <see langword="true"/> matches the common GPT-4o-class chat deployments this platform targets. When <see langword="false"/>, <c>MaxTokensCompatibilityPolicy</c> (infrastructure layer) rewrites the outgoing request JSON to swap the field name.</summary>
    public bool ChatDeploymentSupportsMaxTokens { get; init; } = true;

    /// <summary>TASK-GEN-04: whether <see cref="ChatDeployment"/> reliably supports schema-constrained ("structured output") chat completions. Reliability varies per deployment (D4); default <see langword="false"/> so the guaranteed constrained citation-marker parsing path (TASK-CIT-03) is used unless explicitly opted in for a deployment known to support it well.</summary>
    public bool ChatDeploymentSupportsStructuredOutput { get; init; }

    public int RequestTimeoutSeconds { get; init; } = 60;

    public int MaxRetries { get; init; } = 3;
}
