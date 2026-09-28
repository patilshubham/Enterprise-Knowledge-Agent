using EnterpriseKnowledgeAgent.Domain.Conversations;
using EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;
using EnterpriseKnowledgeAgent.Shared.Agents;
using EnterpriseKnowledgeAgent.Shared.Chat;
using EnterpriseKnowledgeAgent.Shared.Retrieval;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseKnowledgeAgent.Infrastructure.IntegrationTests;

/// <summary>
/// Module CHAT: persistence round-trip tests against a real Postgres container.
/// The <see cref="Appending_messages_to_a_tracked_conversation_marks_them_Added_and_persists"/>
/// case is a regression guard for a real production bug: appending new
/// <see cref="ConversationMessage"/> children to an already change-tracked
/// <see cref="Conversation"/> emitted UPDATEs (matching zero rows -&gt;
/// <see cref="DbUpdateConcurrencyException"/>) instead of INSERTs, because the
/// client-generated Guid key was left as EF's default
/// <c>ValueGeneratedOnAdd</c> convention. This exact flow (load tracked, append
/// turn, save) is what the runtime streaming pipeline performs on every turn,
/// yet it is never exercised by the excluded-from-CI integration suite - which
/// is why the bug reached a live environment before being caught.
/// </summary>
public sealed class ConversationRepositoryTests(PostgresDatabaseFixture fixture) : IClassFixture<PostgresDatabaseFixture>
{
    private static Conversation CreateValid(Guid applicationId) =>
        Conversation.Create(
            applicationId,
            "sub-1",
            KnowledgeScopeType.AllReadyEnabled,
            selectedFileIds: null,
            "gpt-4o",
            0.7,
            RetrievalMode.Hybrid,
            10,
            0.5,
            8000,
            includeNeighbors: true,
            citationsRequired: true,
            systemPrompt: null,
            DateTimeOffset.UtcNow);

    [Fact]
    public async Task Appending_messages_to_a_tracked_conversation_marks_them_Added_and_persists()
    {
        var applicationId = Guid.NewGuid();
        var conversation = CreateValid(applicationId);

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new ConversationRepository(writeContext);
            await repository.AddAsync(conversation);
            await repository.SaveChangesAsync();
        }

        // Reload the conversation change-tracked (with its message collection),
        // exactly as the streaming pipeline does before appending a turn.
        await using (var turnContext = fixture.CreateDbContext())
        {
            var repository = new ConversationRepository(turnContext);
            var loaded = await repository.GetAsync(applicationId, conversation.Id);
            loaded.Should().NotBeNull();

            var now = DateTimeOffset.UtcNow;
            loaded!.StartUserMessage("How much vacation do I get?", now);
            loaded.StartAssistantMessage(now);

            // Regression assertion: the two freshly appended children must be
            // tracked as Added (INSERT), not Modified (UPDATE). This is the crux
            // of the bug the ValueGeneratedNever() mapping fixes.
            turnContext.ChangeTracker.Entries<ConversationMessage>()
                .Select(e => e.State)
                .Should().OnlyContain(state => state == EntityState.Added);

            // Before the fix, this threw DbUpdateConcurrencyException
            // ("expected to affect 1 row(s), but actually affected 0 row(s)").
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var readRepository = new ConversationRepository(readContext);
        var reloaded = await readRepository.GetAsync(applicationId, conversation.Id);

        reloaded.Should().NotBeNull();
        reloaded!.Messages.Should().HaveCount(2);
        reloaded.Messages[0].Role.Should().Be(MessageRole.User);
        reloaded.Messages[0].Content.Should().Be("How much vacation do I get?");
        reloaded.Messages[0].Sequence.Should().Be(0);
        reloaded.Messages[1].Role.Should().Be(MessageRole.Assistant);
        reloaded.Messages[1].Sequence.Should().Be(1);
        reloaded.Title.Should().Be("How much vacation do I get?");
    }

    [Fact]
    public async Task A_completed_turn_round_trips_content_status_and_citations()
    {
        var applicationId = Guid.NewGuid();
        var conversation = CreateValid(applicationId);

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new ConversationRepository(writeContext);
            await repository.AddAsync(conversation);
            await repository.SaveChangesAsync();
        }

        await using (var turnContext = fixture.CreateDbContext())
        {
            var repository = new ConversationRepository(turnContext);
            var loaded = await repository.GetAsync(applicationId, conversation.Id);
            loaded.Should().NotBeNull();

            var now = DateTimeOffset.UtcNow;
            loaded!.StartUserMessage("How much vacation do I get?", now);
            var assistant = loaded.StartAssistantMessage(now);
            assistant.Complete("You get 20 days. [SOURCE-001]", ["SOURCE-001"], now);
            loaded.TouchAfterAnswer(now);

            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var readRepository = new ConversationRepository(readContext);
        var reloaded = await readRepository.GetAsync(applicationId, conversation.Id);

        reloaded.Should().NotBeNull();
        reloaded!.Messages.Should().HaveCount(2);

        var answer = reloaded.Messages[1];
        answer.Role.Should().Be(MessageRole.Assistant);
        answer.Status.Should().Be(MessageStatus.Completed);
        answer.Content.Should().Be("You get 20 days. [SOURCE-001]");
        answer.CitedSourceIds.Should().BeEquivalentTo(["SOURCE-001"]);
        answer.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAsync_returns_null_for_unknown_conversation()
    {
        await using var dbContext = fixture.CreateDbContext();
        var repository = new ConversationRepository(dbContext);

        var loaded = await repository.GetAsync(Guid.NewGuid(), Guid.NewGuid());

        loaded.Should().BeNull();
    }
}
