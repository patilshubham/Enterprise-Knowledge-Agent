using EnterpriseKnowledgeAgent.Application.AzureOpenAI;

namespace EnterpriseKnowledgeAgent.Application.Agents;

/// <summary>
/// TASK-AGT-03: validates an agent's generation parameters against the
/// selected deployment's actual capabilities (FR-PAR-004) - unsupported
/// values are always rejected (never silently ignored or coerced), so the
/// caller always gets an explicit, actionable reason.
/// </summary>
public sealed class AgentParameterValidator(IDeploymentRegistry deploymentRegistry)
{
    /// <summary>
    /// Throws <see cref="AgentValidationException"/> if <paramref name="generationDeployment"/>
    /// does not resolve to a configured <see cref="DeploymentPurpose.Chat"/>
    /// deployment, if <paramref name="maximumOutputTokens"/> exceeds that
    /// deployment's limit, or if <paramref name="topP"/> is supplied for a
    /// deployment that does not support it.
    /// </summary>
    public void ValidateGenerationParameters(string generationDeployment, int maximumOutputTokens, double? topP)
    {
        var deployment = deploymentRegistry.ListDeployments()
            .FirstOrDefault(d => d.Purpose == DeploymentPurpose.Chat
                && string.Equals(d.Name, generationDeployment, StringComparison.OrdinalIgnoreCase));

        if (deployment is null)
        {
            throw new AgentValidationException(
                $"'{generationDeployment}' is not a configured chat/generation deployment.");
        }

        if (deployment.MaxOutputTokens is { } maxOutput && maximumOutputTokens > maxOutput)
        {
            throw new AgentValidationException(
                $"Maximum output tokens ({maximumOutputTokens}) exceeds deployment '{deployment.Name}''s limit of {maxOutput}.");
        }

        if (topP is not null && !deployment.SupportsTopP)
        {
            throw new AgentValidationException(
                $"Deployment '{deployment.Name}' does not support the top_p parameter; leave it unset for this deployment.");
        }
    }
}
