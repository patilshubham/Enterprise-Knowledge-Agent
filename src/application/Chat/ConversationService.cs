using EnterpriseKnowledgeAgent.Application.AzureOpenAI;
using EnterpriseKnowledgeAgent.Domain.Conversations;
using EnterpriseKnowledgeAgent.Shared.Agents;
using EnterpriseKnowledgeAgent.Shared.Retrieval;
using Microsoft.Extensions.Options;

namespace EnterpriseKnowledgeAgent.Application.Chat;

/// <summary>
/// Module CHAT: create/list/read/rename/delete orchestration for shared
/// knowledge-base conversations. Resolves the config-free defaults (and any
/// advanced overrides) into the immutable settings snapshot each conversation
/// runs with. The streaming answer pipeline lives separately in
/// <see cref="ChatCompletionService"/>.
/// </summary>
public sealed class ConversationService(
    IConversationRepository repository,
    IDeploymentRegistry deploymentRegistry,
    IOptions<AoiOptions> aoiOptions,
    TimeProvider timeProvider)
{
    public async Task<ConversationDetailResponse> CreateAsync(
        Guid applicationId,
        string actor,
        CreateConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var advanced = request.Advanced;
        var scopeType = ParseScope(advanced?.KnowledgeScope);
        var selectedFileIds = scopeType == KnowledgeScopeType.Selected ? advanced?.SelectedFileIds : null;
        var deployment = ResolveDeployment(advanced?.GenerationDeployment);
        var temperature = advanced?.Temperature ?? ChatDefaults.Temperature;
        var retrievalMode = ParseRetrievalMode(advanced?.RetrievalMode);
        var systemPrompt = advanced?.SystemPrompt;

        var conversation = Conversation.Create(
            applicationId,
            actor,
            scopeType,
            selectedFileIds,
            deployment,
            temperature,
            retrievalMode,
            ChatDefaults.RetrievalCount,
            ChatDefaults.MinimumSimilarity,
            ChatDefaults.MaximumEvidenceTokens,
            ChatDefaults.IncludeNeighbors,
            ChatDefaults.CitationsRequired,
            systemPrompt,
            timeProvider.GetUtcNow());

        await repository.AddAsync(conversation, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return MapDetail(conversation, []);
    }

    public async Task<IReadOnlyList<ConversationSummaryResponse>> ListAsync(
        Guid applicationId,
        CancellationToken cancellationToken = default)
    {
        var items = await repository.ListAsync(applicationId, cancellationToken);
        return items
            .Select(i => new ConversationSummaryResponse(i.Id, i.Title, i.CreatedBy, i.CreatedAt, i.LastMessageAt, i.MessageCount))
            .ToList();
    }

    public async Task<ConversationDetailResponse> GetDetailAsync(
        Guid applicationId,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var conversation = await repository.GetAsync(applicationId, conversationId, cancellationToken)
            ?? throw new ConversationNotFoundException(conversationId);
        var evidence = await repository.GetEvidenceAsync(conversationId, cancellationToken);
        return MapDetail(conversation, evidence);
    }

    /// <summary>Renames an already-loaded, change-tracked conversation (the caller has performed creator-or-admin authorization).</summary>
    public async Task<ConversationSummaryResponse> RenameAsync(
        Conversation conversation,
        string title,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        conversation.Rename(title, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return new ConversationSummaryResponse(
            conversation.Id, conversation.Title, conversation.CreatedBy, conversation.CreatedAt,
            conversation.LastMessageAt, conversation.Messages.Count);
    }

    /// <summary>Soft-deletes an already-loaded, change-tracked conversation (the caller has performed creator-or-admin authorization).</summary>
    public async Task DeleteAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        conversation.SoftDelete(timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
    }

    private string ResolveDeployment(string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            return aoiOptions.Value.ChatDeployment;
        }

        var trimmed = requested.Trim();
        var isChatDeployment = deploymentRegistry.ListDeployments()
            .Any(d => d.Purpose == DeploymentPurpose.Chat && string.Equals(d.Name, trimmed, StringComparison.OrdinalIgnoreCase));
        if (!isChatDeployment)
        {
            throw new ArgumentException($"'{trimmed}' is not a configured chat deployment.", nameof(requested));
        }

        return trimmed;
    }

    private static KnowledgeScopeType ParseScope(string? raw) => (raw?.Trim().ToLowerInvariant()) switch
    {
        null or "" or "all" or "allreadyenabled" => KnowledgeScopeType.AllReadyEnabled,
        "selected" => KnowledgeScopeType.Selected,
        _ => throw new ArgumentException($"Unknown knowledge scope '{raw}'.", nameof(raw)),
    };

    private static RetrievalMode ParseRetrievalMode(string? raw) => (raw?.Trim().ToLowerInvariant()) switch
    {
        null or "" or "semantic" => RetrievalMode.Semantic,
        "hybrid" => RetrievalMode.Hybrid,
        _ => throw new ArgumentException($"Unknown retrieval mode '{raw}'.", nameof(raw)),
    };

    private static ConversationDetailResponse MapDetail(
        Conversation conversation,
        IReadOnlyList<ConversationMessageEvidence> evidence)
    {
        var evidenceByMessage = evidence
            .GroupBy(e => e.ConversationMessageId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ConversationMessageEvidence>)g.ToList());

        var messages = conversation.Messages
            .OrderBy(m => m.Sequence)
            .Select(m => MapMessage(m, evidenceByMessage.GetValueOrDefault(m.Id, [])))
            .ToList();

        return new ConversationDetailResponse(
            conversation.Id,
            conversation.Title,
            conversation.CreatedBy,
            conversation.CreatedAt,
            conversation.LastMessageAt,
            MapSettings(conversation),
            messages);
    }

    private static ConversationSettingsResponse MapSettings(Conversation conversation) => new(
        conversation.KnowledgeScopeType.ToString(),
        conversation.SelectedFileIds,
        conversation.GenerationDeployment,
        conversation.Temperature,
        conversation.RetrievalMode.ToString(),
        conversation.SystemPrompt is not null);

    private static ConversationMessageResponse MapMessage(
        ConversationMessage message,
        IReadOnlyList<ConversationMessageEvidence> evidence) => new(
        message.Id,
        message.Role.ToString(),
        message.Content,
        message.Status.ToString(),
        message.Sequence,
        message.CreatedAt,
        message.CompletedAt,
        message.FailureCategory?.ToString(),
        message.FailureMessage,
        message.CitedSourceIds,
        evidence.Select(MapEvidence).ToList());

    internal static ChatEvidenceResponse MapEvidence(ConversationMessageEvidence evidence) => new(
        evidence.SourceId,
        evidence.KnowledgeFileId,
        evidence.KnowledgeChunkId,
        evidence.FileNameSnapshot,
        evidence.HeadingPathSnapshot,
        evidence.PassageSnapshot,
        evidence.CharacterStartSnapshot,
        evidence.CharacterEndSnapshot,
        SourceUnavailable: false);
}
