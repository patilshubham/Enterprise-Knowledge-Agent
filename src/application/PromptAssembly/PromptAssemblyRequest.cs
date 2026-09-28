using EnterpriseKnowledgeAgent.Application.Retrieval;

namespace EnterpriseKnowledgeAgent.Application.PromptAssembly;

/// <summary>TASK-PRMT-02: what the worker (TASK-RUN-02) supplies to assemble one run's request.</summary>
/// <param name="AgentPrompt">The agent's configured prompt snapshot, verbatim - never truncated (FR-AGT-003).</param>
/// <param name="UserRequest">The user's run request text, verbatim.</param>
/// <param name="EvidenceSources">
/// The evidence package selected by retrieval (module 16) within the budget computed by
/// <see cref="ContextBudgetCalculator"/> (TASK-PRMT-01) - may be empty when no evidence was found
/// or matched (module 16's <c>NoEvidence</c>); the worker decides whether that is acceptable
/// (TASK-RET-05's <c>StrictGrounding</c>), this type only renders whatever it is given.
/// </param>
public sealed record PromptAssemblyRequest(
    string AgentPrompt,
    string UserRequest,
    IReadOnlyList<EvidenceSource> EvidenceSources);

/// <summary>
/// TASK-PRMT-02: the assembled, layered request ready to send to
/// <see cref="EnterpriseKnowledgeAgent.Application.AzureOpenAI.ITextGenerationService"/>.
/// </summary>
/// <param name="Messages">The ordered chat messages - platform instructions, agent prompt, evidence, user request, output protocol.</param>
public sealed record AssembledPrompt(IReadOnlyList<EnterpriseKnowledgeAgent.Application.AzureOpenAI.ChatMessage> Messages);

/// <summary>
/// Module CHAT: what the chat pipeline supplies to assemble one conversational
/// turn. Same layered/injection-resistant structure as
/// <see cref="PromptAssemblyRequest"/>, but with prior conversation turns
/// inserted between the freshly-retrieved evidence and the current question so
/// the assistant has short-term memory of the thread.
/// </summary>
/// <param name="SystemPrompt">The conversation's assistant persona (or the platform default), verbatim.</param>
/// <param name="History">Prior turns of the conversation, already role-mapped ("user"/"assistant"), oldest first - excludes the current question.</param>
/// <param name="UserRequest">The current user question, verbatim.</param>
/// <param name="EvidenceSources">Evidence retrieved for the current question - may be empty.</param>
public sealed record ChatPromptRequest(
    string SystemPrompt,
    IReadOnlyList<EnterpriseKnowledgeAgent.Application.AzureOpenAI.ChatMessage> History,
    string UserRequest,
    IReadOnlyList<EvidenceSource> EvidenceSources);
