using EnterpriseKnowledgeAgent.Application.AzureOpenAI;
using EnterpriseKnowledgeAgent.Application.Chat;
using EnterpriseKnowledgeAgent.Application.Retrieval;
using EnterpriseKnowledgeAgent.Domain.Conversations;
using EnterpriseKnowledgeAgent.Shared.Agents;
using EnterpriseKnowledgeAgent.Shared.Chat;
using EnterpriseKnowledgeAgent.Shared.Errors;
using EnterpriseKnowledgeAgent.Shared.Retrieval;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace EnterpriseKnowledgeAgent.Application.Tests.Chat;

/// <summary>Module CHAT: unit tests for the <see cref="ChatCompletionService"/> streaming retrieve-and-generate pipeline.</summary>
public sealed class ChatCompletionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static Conversation CreateConversation() =>
        Conversation.Create(
            applicationId: Guid.NewGuid(),
            createdBy: "keycloak-sub-1",
            knowledgeScopeType: KnowledgeScopeType.AllReadyEnabled,
            selectedFileIds: null,
            generationDeployment: "gpt-4o",
            temperature: 0.2,
            retrievalMode: RetrievalMode.Semantic,
            retrievalCount: 8,
            minimumSimilarity: 0.3,
            maximumEvidenceTokens: 6_000,
            includeNeighbors: true,
            citationsRequired: true,
            systemPrompt: null,
            now: Now);

    private static EvidenceSource Source(string sourceId) =>
        new(sourceId, Guid.NewGuid(), Guid.NewGuid(), "company-handbook.txt", "Leave > Vacation",
            "Employees accrue 20 days of vacation per year.", 0, 50, 12, 0.91);

    private static async Task<List<ChatStreamEvent>> DrainAsync(
        ChatCompletionService service, Conversation conversation, string question, CancellationToken cancellationToken = default)
    {
        var events = new List<ChatStreamEvent>();
        await foreach (var streamEvent in service.StreamAnswerAsync(conversation, question, cancellationToken))
        {
            events.Add(streamEvent);
        }

        return events;
    }

    [Fact]
    public async Task StreamAnswerAsync_streams_tokens_then_a_done_event_with_validated_citations()
    {
        var conversation = CreateConversation();
        var repository = new FakeConversationRepository(conversation);
        var retrieval = new FakeRetrievalService(new RetrievalResult(false, [Source("SOURCE-001")], []));
        var generation = new FakeTextGenerationService("You accrue 20 days. ", "[SOURCE-001]");
        var service = new ChatCompletionService(repository, retrieval, generation, new FakeTimeProvider(Now), NullLogger<ChatCompletionService>.Instance);

        var events = await DrainAsync(service, conversation, "How much vacation do I accrue?");

        events.OfType<ChatTokenEvent>().Should().NotBeEmpty();
        var done = events.OfType<ChatDoneEvent>().Should().ContainSingle().Subject;
        done.Status.Should().Be(MessageStatus.Completed.ToString());
        done.CitedSourceIds.Should().ContainSingle().Which.Should().Be("SOURCE-001");
        done.Evidence.Should().ContainSingle();

        var assistant = conversation.Messages.Single(m => m.Role == MessageRole.Assistant);
        assistant.Status.Should().Be(MessageStatus.Completed);
        assistant.Content.Should().Be("You accrue 20 days. [SOURCE-001]");
        repository.AddedEvidence.Should().ContainSingle();
    }

    [Fact]
    public async Task StreamAnswerAsync_ignores_citation_markers_for_sources_not_retrieved_this_turn()
    {
        var conversation = CreateConversation();
        var repository = new FakeConversationRepository(conversation);
        var retrieval = new FakeRetrievalService(new RetrievalResult(false, [Source("SOURCE-001")], []));
        // The model hallucinates a SOURCE-002 that was never in the evidence package.
        var generation = new FakeTextGenerationService("Answer [SOURCE-001] and [SOURCE-002].");
        var service = new ChatCompletionService(repository, retrieval, generation, new FakeTimeProvider(Now), NullLogger<ChatCompletionService>.Instance);

        var events = await DrainAsync(service, conversation, "Question");

        var done = events.OfType<ChatDoneEvent>().Should().ContainSingle().Subject;
        done.CitedSourceIds.Should().ContainSingle().Which.Should().Be("SOURCE-001");
    }

    [Fact]
    public async Task StreamAnswerAsync_returns_a_friendly_no_results_answer_when_retrieval_finds_nothing()
    {
        var conversation = CreateConversation();
        var repository = new FakeConversationRepository(conversation);
        var retrieval = new FakeRetrievalService(new RetrievalResult(true, [], []));
        var generation = new FakeTextGenerationService("SHOULD NOT BE CALLED");
        var service = new ChatCompletionService(repository, retrieval, generation, new FakeTimeProvider(Now), NullLogger<ChatCompletionService>.Instance);

        var events = await DrainAsync(service, conversation, "Something off-topic");

        generation.WasCalled.Should().BeFalse();
        events.OfType<ChatTokenEvent>().Should().ContainSingle()
            .Which.Text.Should().Be(ChatDefaults.NoEvidenceMessage);
        var done = events.OfType<ChatDoneEvent>().Should().ContainSingle().Subject;
        done.Status.Should().Be(MessageStatus.NoResults.ToString());

        var assistant = conversation.Messages.Single(m => m.Role == MessageRole.Assistant);
        assistant.Status.Should().Be(MessageStatus.NoResults);
    }

    [Fact]
    public async Task StreamAnswerAsync_emits_an_error_event_and_marks_the_message_failed_when_generation_throws()
    {
        var conversation = CreateConversation();
        var repository = new FakeConversationRepository(conversation);
        var retrieval = new FakeRetrievalService(new RetrievalResult(false, [Source("SOURCE-001")], []));
        var generation = new FakeTextGenerationService("partial")
        {
            ThrowAfterFirstToken = new AzureOpenAiException(ErrorCategory.Throttled, "429 Too Many Requests"),
        };
        var service = new ChatCompletionService(repository, retrieval, generation, new FakeTimeProvider(Now), NullLogger<ChatCompletionService>.Instance);

        var events = await DrainAsync(service, conversation, "Question");

        var error = events.OfType<ChatErrorEvent>().Should().ContainSingle().Subject;
        error.Category.Should().Be(ErrorCategory.Throttled.ToString());
        error.Message.Should().NotBeNullOrWhiteSpace();

        var assistant = conversation.Messages.Single(m => m.Role == MessageRole.Assistant);
        assistant.Status.Should().Be(MessageStatus.Failed);
        assistant.FailureCategory.Should().Be(ErrorCategory.Throttled);
    }

    [Fact]
    public async Task StreamAnswerAsync_marks_the_message_cancelled_when_the_client_disconnects()
    {
        var conversation = CreateConversation();
        var repository = new FakeConversationRepository(conversation);
        var retrieval = new FakeRetrievalService(new RetrievalResult(false, [Source("SOURCE-001")], []));
        using var cts = new CancellationTokenSource();
        var generation = new FakeTextGenerationService("first ", "second")
        {
            CancelAfterFirstToken = cts,
        };
        var service = new ChatCompletionService(repository, retrieval, generation, new FakeTimeProvider(Now), NullLogger<ChatCompletionService>.Instance);

        // The pipeline observes the cancellation, finalizes the in-flight
        // message as Cancelled, and completes the stream without surfacing an
        // exception to the caller.
        var events = await DrainAsync(service, conversation, "Question", cts.Token);

        events.OfType<ChatDoneEvent>().Should().BeEmpty();
        var assistant = conversation.Messages.Single(m => m.Role == MessageRole.Assistant);
        assistant.Status.Should().Be(MessageStatus.Cancelled);
        assistant.Content.Should().Be("first ");
    }

    // --- Fakes ---

    private sealed class FakeConversationRepository(Conversation conversation) : IConversationRepository
    {
        private readonly List<ConversationMessageEvidence> _evidence = [];

        public IReadOnlyList<ConversationMessageEvidence> AddedEvidence => _evidence;

        public Task AddAsync(Conversation newConversation, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<Conversation?> GetAsync(Guid applicationId, Guid conversationId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Conversation?>(conversation);

        public Task<IReadOnlyList<ConversationListItem>> ListAsync(Guid applicationId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ConversationListItem>>([]);

        public Task<IReadOnlyList<ConversationMessageEvidence>> GetEvidenceAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ConversationMessageEvidence>>(_evidence);

        public Task AddEvidenceAsync(IReadOnlyList<ConversationMessageEvidence> evidence, CancellationToken cancellationToken = default)
        {
            _evidence.AddRange(evidence);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeRetrievalService(RetrievalResult result) : IRetrievalService
    {
        public Task<RetrievalResult> RetrieveAsync(RetrievalRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    private sealed class FakeTextGenerationService(params string[] tokens) : ITextGenerationService
    {
        public bool WasCalled { get; private set; }

        public Exception? ThrowAfterFirstToken { get; set; }

        public CancellationTokenSource? CancelAfterFirstToken { get; set; }

        public Task<TextGenerationResult> GenerateAsync(IReadOnlyList<ChatMessage> messages, string deployment, GenerationParameters? parameters = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TextGenerationResult(string.Concat(tokens), "stop"));

        public async IAsyncEnumerable<string> GenerateStreamingAsync(IReadOnlyList<ChatMessage> messages, string deployment, GenerationParameters? parameters = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            var index = 0;
            foreach (var token in tokens)
            {
                await Task.Yield();
                yield return token;
                index++;

                if (index == 1 && CancelAfterFirstToken is not null)
                {
                    CancelAfterFirstToken.Cancel();
                    cancellationToken.ThrowIfCancellationRequested();
                }

                if (index == 1 && ThrowAfterFirstToken is not null)
                {
                    throw ThrowAfterFirstToken;
                }
            }
        }

        public Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
