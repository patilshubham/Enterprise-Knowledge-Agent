using System.Text.Json;
using EnterpriseKnowledgeAgent.Application.AzureOpenAI;
using EnterpriseKnowledgeAgent.Application.Execution;
using EnterpriseKnowledgeAgent.Application.PromptAssembly;
using EnterpriseKnowledgeAgent.Domain.Execution;
using EnterpriseKnowledgeAgent.Shared.Errors;

namespace EnterpriseKnowledgeAgent.Application.Generation;

/// <summary>TASK-GEN-01: the raw generation result before Markdown/citation validation (TASK-GEN-02/CIT-03 take it from here).</summary>
/// <param name="Content">The raw generated text, exactly as returned by the model - preserved immutably (FR-OUT-003) before any extraction/validation.</param>
/// <param name="FinishReason">Provider-reported finish reason (e.g. "stop", "length", "content_filter").</param>
/// <param name="EffectiveParameters">The parameters actually sent to the model (may differ from requested - e.g. TopP dropped for a deployment that doesn't support it).</param>
public sealed record GenerationOutcome(string Content, string? FinishReason, AgentRunParametersSnapshot EffectiveParameters);

/// <summary>
/// TASK-GEN-01: sends the assembled request (TASK-PRMT-02) to the run's
/// snapshotted chat deployment, applying the run's requested parameters
/// (dropping <c>TopP</c> if the deployment doesn't support it), and records
/// the effective parameter snapshot on the run (requested was already
/// recorded at submission time - TASK-RUN-01).
/// </summary>
public sealed class GenerationService(ITextGenerationService textGenerationService, IDeploymentRegistry deploymentRegistry)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>TASK-GEN-04: the shape requested from a structured-output-capable deployment; property names match <see cref="JsonOptions"/>'s camelCase policy against the schema the infrastructure-layer text generation service requests.</summary>
    private sealed record StructuredMarkdownResponse(string? Markdown);


    public async Task<GenerationOutcome> GenerateAsync(AgentRun run, AssembledPrompt prompt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(prompt);

        if (string.IsNullOrWhiteSpace(run.DeploymentSnapshot))
        {
            throw new AgentRunExecutionException(ErrorCategory.Configuration, $"Run '{run.Id}' has no deployment snapshot recorded.");
        }

        if (run.RequestedParametersJson is null)
        {
            throw new AgentRunExecutionException(ErrorCategory.Configuration, $"Run '{run.Id}' has no requested parameters recorded.");
        }

        var deployment = deploymentRegistry.ListDeployments()
            .FirstOrDefault(d => d.Purpose == DeploymentPurpose.Chat
                && string.Equals(d.Name, run.DeploymentSnapshot, StringComparison.OrdinalIgnoreCase));

        if (deployment is null)
        {
            throw new AgentRunExecutionException(ErrorCategory.Configuration, $"'{run.DeploymentSnapshot}' is not a configured chat/generation deployment.");
        }

        var requested = JsonSerializer.Deserialize<AgentRunParametersSnapshot>(run.RequestedParametersJson, JsonOptions)
            ?? throw new AgentRunExecutionException(ErrorCategory.Configuration, $"Run '{run.Id}' has malformed requested parameters.");

        var effectiveTopP = deployment.SupportsTopP ? requested.TopP : null;
        var effective = requested with { TopP = effectiveTopP };
        var requestStructuredOutput = deployment.SupportsStructuredOutput;
        var generationParameters = new GenerationParameters(requested.Temperature, requested.MaximumOutputTokens, effectiveTopP, requestStructuredOutput);

        TextGenerationResult result;
        try
        {
            result = await textGenerationService.GenerateAsync(prompt.Messages, run.DeploymentSnapshot, generationParameters, cancellationToken).ConfigureAwait(false);
        }
        catch (AzureOpenAiException ex)
        {
            // TASK-RUN-02's worker catches AgentRunExecutionException generically -
            // re-wrap so every pipeline stage fails through a single exception type.
            throw new AgentRunExecutionException(ex.Category, ex.Message, ex);
        }

        run.RecordEffectiveParameters(JsonSerializer.Serialize(effective, JsonOptions));

        var content = requestStructuredOutput ? ExtractMarkdown(result.Content) : result.Content;

        return new GenerationOutcome(content, result.FinishReason, effective);
    }

    /// <summary>TASK-GEN-04: maps a structured-output response (a JSON object of shape <c>{ "markdown": "..." }</c>) to its Markdown content; on any parse failure or a missing/empty field, falls back to using the raw response content directly (D4: constrained citation-marker parsing, module 22, remains the guaranteed path regardless of which text this returns).</summary>
    private static string ExtractMarkdown(string content)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<StructuredMarkdownResponse>(content, JsonOptions);
            if (!string.IsNullOrWhiteSpace(parsed?.Markdown))
            {
                return parsed.Markdown;
            }
        }
        catch (JsonException)
        {
            // Fall through - treat the raw content as the Markdown itself.
        }

        return content;
    }
}
