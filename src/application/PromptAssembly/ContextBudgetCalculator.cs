using EnterpriseKnowledgeAgent.Application.AzureOpenAI;
using EnterpriseKnowledgeAgent.Application.Execution;
using EnterpriseKnowledgeAgent.Application.Tokenization;
using EnterpriseKnowledgeAgent.Shared.Errors;

namespace EnterpriseKnowledgeAgent.Application.PromptAssembly;

/// <summary>
/// TASK-PRMT-01: computes the evidence-token budget available to retrieval
/// (module 16) and rejects impossible configurations before any AOAI call -
/// available evidence tokens = deployment context capacity − platform
/// instructions − agent prompt − user request − protocol overhead − output
/// reservation. The agent prompt is never truncated to make room (FR-AGT-003) -
/// an over-budget fixed layer is a terminal <see cref="ErrorCategory.ContextOverflow"/>
/// failure instead.
/// </summary>
public sealed class ContextBudgetCalculator(IDeploymentRegistry deploymentRegistry, ITokenEstimator tokenEstimator)
{
    public ContextBudgetResult Calculate(ContextBudgetRequest request)
    {
        var deployment = deploymentRegistry.ListDeployments()
            .FirstOrDefault(d => d.Purpose == DeploymentPurpose.Chat
                && string.Equals(d.Name, request.GenerationDeployment, StringComparison.OrdinalIgnoreCase));

        if (deployment is null)
        {
            throw new AgentRunExecutionException(
                ErrorCategory.Configuration,
                $"'{request.GenerationDeployment}' is not a configured chat/generation deployment.");
        }

        if (deployment.MaxContextTokens is not { } contextLimit)
        {
            throw new AgentRunExecutionException(
                ErrorCategory.Configuration,
                $"Deployment '{deployment.Name}' does not declare a context token limit.");
        }

        var fixedLayerTokens =
            tokenEstimator.EstimateTokenCount(request.PlatformInstructions, request.GenerationDeployment) +
            tokenEstimator.EstimateTokenCount(request.AgentPrompt, request.GenerationDeployment) +
            tokenEstimator.EstimateTokenCount(request.UserRequest, request.GenerationDeployment) +
            tokenEstimator.EstimateTokenCount(request.ProtocolInstructions, request.GenerationDeployment) +
            request.MaximumOutputTokens;

        if (fixedLayerTokens > contextLimit)
        {
            throw new AgentRunExecutionException(
                ErrorCategory.ContextOverflow,
                $"The platform instructions, agent prompt, user request, output protocol, and reserved output tokens " +
                $"together ({fixedLayerTokens} tokens) exceed deployment '{deployment.Name}''s context limit of {contextLimit} tokens.");
        }

        var evidenceTokenBudget = Math.Min(contextLimit - fixedLayerTokens, request.MaximumEvidenceTokens);
        return new ContextBudgetResult(evidenceTokenBudget, fixedLayerTokens, contextLimit);
    }
}
