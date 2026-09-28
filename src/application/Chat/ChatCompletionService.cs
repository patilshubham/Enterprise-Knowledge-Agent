using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using EnterpriseKnowledgeAgent.Application.AzureOpenAI;
using EnterpriseKnowledgeAgent.Application.PromptAssembly;
using EnterpriseKnowledgeAgent.Application.Retrieval;
using EnterpriseKnowledgeAgent.Domain.Conversations;
using EnterpriseKnowledgeAgent.Shared.Agents;
using EnterpriseKnowledgeAgent.Shared.Chat;
using EnterpriseKnowledgeAgent.Shared.Errors;
using Microsoft.Extensions.Logging;

namespace EnterpriseKnowledgeAgent.Application.Chat;

/// <summary>
/// Module CHAT: the runtime retrieve-and-generate pipeline for one
/// conversational turn. Persists the user question and a streaming assistant
/// placeholder, retrieves grounding evidence from the knowledge base, assembles
/// the injection-resistant prompt (with short-term conversation memory),
/// streams the model's answer token-by-token, then validates citation markers
/// and snapshots the cited evidence. Every terminal state (completed, no
/// results, failed, cancelled) is persisted so the conversation survives a
/// client disconnect or a page reload.
/// </summary>
public sealed partial class ChatCompletionService(
    IConversationRepository repository,
    IRetrievalService retrievalService,
    ITextGenerationService textGenerationService,
    TimeProvider timeProvider,
    ILogger<ChatCompletionService> logger)
{
    [GeneratedRegex(@"\[SOURCE-\d{3}\]", RegexOptions.Compiled)]
    private static partial Regex SourceMarkerPattern();

    /// <summary>
    /// Runs one turn against an already-loaded, change-tracked conversation and
    /// streams the answer as a sequence of <see cref="ChatStreamEvent"/>s. The
    /// caller (API layer) has already validated access and the request body.
    /// </summary>
    public async IAsyncEnumerable<ChatStreamEvent> StreamAnswerAsync(
        Conversation conversation,
        string userContent,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        var startedAt = timeProvider.GetUtcNow();
        conversation.StartUserMessage(userContent, startedAt);
        var assistant = conversation.StartAssistantMessage(startedAt);
        await repository.SaveChangesAsync(cancellationToken);

        var buffer = new StringBuilder();
        try
        {
            // --- Retrieval ---
            RetrievalResult? retrieval = null;
            ChatStreamEvent? terminal = null;
            var cancelled = false;
            try
            {
                retrieval = await retrievalService.RetrieveAsync(
                    BuildRetrievalRequest(conversation, userContent), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
            catch (Exception ex)
            {
                terminal = await FinalizeFailureAsync(conversation, assistant, ex);
            }

            if (cancelled)
            {
                await FinalizeCancelAsync(conversation, assistant, string.Empty);
                yield break;
            }

            if (terminal is not null)
            {
                yield return terminal;
                yield break;
            }

            // --- No usable evidence: friendly, grounded "no results" answer ---
            if (retrieval!.NoEvidence || retrieval.Sources.Count == 0)
            {
                var noResults = await FinalizeNoResultsAsync(conversation, assistant);
                yield return new ChatTokenEvent(ChatDefaults.NoEvidenceMessage);
                yield return noResults;
                yield break;
            }

            // --- Prompt assembly (safety layers + memory + evidence) ---
            var prompt = PromptAssembler.AssembleChat(new ChatPromptRequest(
                conversation.SystemPrompt ?? ChatDefaults.DefaultSystemPrompt,
                BuildHistory(conversation, assistant),
                userContent,
                retrieval.Sources));

            var parameters = new GenerationParameters(conversation.Temperature, ChatDefaults.MaxOutputTokens, TopP: null);

            // --- Stream generation ---
            await using var enumerator = textGenerationService
                .GenerateStreamingAsync(prompt.Messages, conversation.GenerationDeployment, parameters, cancellationToken)
                .GetAsyncEnumerator(cancellationToken);

            while (true)
            {
                string? token = null;
                var completed = false;
                cancelled = false;
                terminal = null;
                try
                {
                    if (!await enumerator.MoveNextAsync())
                    {
                        completed = true;
                    }
                    else
                    {
                        token = enumerator.Current;
                    }
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                }
                catch (Exception ex)
                {
                    terminal = await FinalizeFailureAsync(conversation, assistant, ex);
                }

                if (cancelled)
                {
                    await FinalizeCancelAsync(conversation, assistant, buffer.ToString());
                    yield break;
                }

                if (terminal is not null)
                {
                    yield return terminal;
                    yield break;
                }

                if (completed)
                {
                    break;
                }

                if (!string.IsNullOrEmpty(token))
                {
                    buffer.Append(token);
                    yield return new ChatTokenEvent(token);
                }
            }

            // --- Finalize: validate citations + snapshot cited evidence ---
            yield return await FinalizeSuccessAsync(conversation, assistant, retrieval.Sources, buffer.ToString());
        }
        finally
        {
            // Safety net: if the consumer abandoned enumeration (e.g. the client
            // disconnected mid-write) the assistant would still be Streaming -
            // persist it as Cancelled so no message is ever left dangling.
            if (assistant.Status == MessageStatus.Streaming)
            {
                var now = timeProvider.GetUtcNow();
                assistant.Cancel(buffer.ToString(), now);
                conversation.TouchAfterAnswer(now);
                await repository.SaveChangesAsync(CancellationToken.None);
            }
        }
    }

    private static RetrievalRequest BuildRetrievalRequest(Conversation conversation, string query)
    {
        var settings = new RetrievalSettings(
            conversation.RetrievalCount,
            conversation.MinimumSimilarity,
            ChatDefaults.SemanticWeight,
            ChatDefaults.KeywordWeight,
            conversation.IncludeNeighbors,
            conversation.MaximumEvidenceTokens,
            StrictGrounding: true,
            conversation.RetrievalMode);

        var selectedFileIds = conversation.KnowledgeScopeType == KnowledgeScopeType.Selected
            ? conversation.SelectedFileIds
            : null;

        return new RetrievalRequest(conversation.ApplicationId, query, settings, selectedFileIds);
    }

    private static List<ChatMessage> BuildHistory(Conversation conversation, ConversationMessage currentAssistant)
    {
        // The current turn is the user message immediately before the assistant
        // placeholder; it is supplied separately as the request, so exclude it
        // (and the placeholder) from the replayed memory window.
        var currentUserSequence = currentAssistant.Sequence - 1;

        return conversation.Messages
            .Where(m => m.Sequence < currentUserSequence)
            .Where(m => m.Role == MessageRole.User || (m.Role == MessageRole.Assistant && m.Status == MessageStatus.Completed))
            .Where(m => !string.IsNullOrWhiteSpace(m.Content))
            .OrderBy(m => m.Sequence)
            .TakeLast(ChatDefaults.HistoryWindowMessages)
            .Select(m => new ChatMessage(m.Role == MessageRole.User ? "user" : "assistant", m.Content))
            .ToList();
    }

    private async Task<ChatDoneEvent> FinalizeNoResultsAsync(Conversation conversation, ConversationMessage assistant)
    {
        var now = timeProvider.GetUtcNow();
        assistant.SetNoResults(ChatDefaults.NoEvidenceMessage, now);
        conversation.TouchAfterAnswer(now);
        await repository.SaveChangesAsync(CancellationToken.None);
        return new ChatDoneEvent(assistant.Id, assistant.Status.ToString(), [], []);
    }

    private async Task<ChatStreamEvent> FinalizeSuccessAsync(
        Conversation conversation,
        ConversationMessage assistant,
        IReadOnlyList<EvidenceSource> sources,
        string content)
    {
        var now = timeProvider.GetUtcNow();

        if (string.IsNullOrWhiteSpace(content))
        {
            const string emptyMessage = "The assistant returned an empty answer. Please try again.";
            assistant.Fail(ErrorCategory.GenerationFailure, emptyMessage, now);
            await repository.SaveChangesAsync(CancellationToken.None);
            return new ChatErrorEvent(assistant.Id, ErrorCategory.GenerationFailure.ToString(), emptyMessage);
        }

        var sourcesById = sources.ToDictionary(s => s.SourceId);
        var citedSourceIds = new List<string>();
        var evidenceRows = new List<ConversationMessageEvidence>();
        var evidenceResponses = new List<ChatEvidenceResponse>();

        foreach (Match match in SourceMarkerPattern().Matches(content))
        {
            var sourceId = match.Value[1..^1];
            // Only trust markers that name a source actually supplied this turn -
            // never the model's own claims about which files it used.
            if (!sourcesById.TryGetValue(sourceId, out var source) || citedSourceIds.Contains(sourceId))
            {
                continue;
            }

            citedSourceIds.Add(sourceId);
            var row = ConversationMessageEvidence.Create(
                conversation.Id,
                assistant.Id,
                source.SourceId,
                source.ChunkId,
                source.FileId,
                source.DisplayName,
                source.HeadingPath,
                source.Content,
                source.CharacterStart,
                source.CharacterEnd,
                now);
            evidenceRows.Add(row);
            evidenceResponses.Add(ConversationService.MapEvidence(row));
        }

        assistant.Complete(content, citedSourceIds, now);
        if (evidenceRows.Count > 0)
        {
            await repository.AddEvidenceAsync(evidenceRows, CancellationToken.None);
        }

        conversation.TouchAfterAnswer(now);
        await repository.SaveChangesAsync(CancellationToken.None);
        return new ChatDoneEvent(assistant.Id, assistant.Status.ToString(), citedSourceIds, evidenceResponses);
    }

    private async Task<ChatErrorEvent> FinalizeFailureAsync(Conversation conversation, ConversationMessage assistant, Exception exception)
    {
        var category = exception is AzureOpenAiException aoai ? aoai.Category : ErrorCategory.GenerationFailure;
        var message = ToUserMessage(category);
        LogChatTurnFailed(conversation.Id, category.ToString(), exception);
        assistant.Fail(category, message, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(CancellationToken.None);
        return new ChatErrorEvent(assistant.Id, category.ToString(), message);
    }

    private async Task FinalizeCancelAsync(Conversation conversation, ConversationMessage assistant, string partial)
    {
        var now = timeProvider.GetUtcNow();
        assistant.Cancel(partial, now);
        conversation.TouchAfterAnswer(now);
        await repository.SaveChangesAsync(CancellationToken.None);
    }

    private static string ToUserMessage(ErrorCategory category) => category switch
    {
        ErrorCategory.Throttled => "The assistant is busy right now. Please try again in a moment.",
        ErrorCategory.Timeout => "The assistant took too long to respond. Please try again.",
        ErrorCategory.DependencyUnavailable => "The assistant is temporarily unavailable. Please try again shortly.",
        ErrorCategory.ContextOverflow => "That question needs too much context to answer. Try asking something more specific.",
        ErrorCategory.Configuration => "The assistant is not configured correctly. Please contact an administrator.",
        _ => "Something went wrong while generating the answer. Please try again.",
    };

    [LoggerMessage(EventId = 5100, Level = LogLevel.Error, Message = "Chat turn failed for conversation {ConversationId} (category {Category}).")]
    private partial void LogChatTurnFailed(Guid conversationId, string category, Exception exception);
}
