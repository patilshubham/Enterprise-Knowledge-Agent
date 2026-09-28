namespace EnterpriseKnowledgeAgent.Application.PromptAssembly;

/// <summary>
/// TASK-PRMT-01: inputs for <see cref="ContextBudgetCalculator.Calculate"/> -
/// the already-rendered text of every fixed (non-evidence) prompt layer, plus
/// the reservations that must fit alongside the evidence budget. The exact
/// wording of <see cref="PlatformInstructions"/>/<see cref="ProtocolInstructions"/>
/// is owned by <c>PromptAssembler</c> (TASK-PRMT-02) - this type only does
/// token math, it never authors prompt text itself.
/// </summary>
/// <param name="PlatformInstructions">The fixed platform safety/grounding instructions layer.</param>
/// <param name="AgentPrompt">The agent's configured prompt snapshot - never truncated (FR-AGT-003).</param>
/// <param name="UserRequest">The user's run request text.</param>
/// <param name="ProtocolInstructions">The Markdown+citation output-protocol instructions layer.</param>
/// <param name="MaximumOutputTokens">The agent's configured output-token reservation.</param>
/// <param name="MaximumEvidenceTokens">The agent's configured cap on evidence tokens (an upper bound - the deployment's remaining context may allow less).</param>
/// <param name="GenerationDeployment">The agent's configured chat deployment name.</param>
public sealed record ContextBudgetRequest(
    string PlatformInstructions,
    string AgentPrompt,
    string UserRequest,
    string ProtocolInstructions,
    int MaximumOutputTokens,
    int MaximumEvidenceTokens,
    string GenerationDeployment);

/// <summary>TASK-PRMT-01: the computed budget - <see cref="EvidenceTokenBudget"/> is what module 16's retrieval consumes.</summary>
public sealed record ContextBudgetResult(int EvidenceTokenBudget, int FixedLayerTokens, int DeploymentContextTokens);
