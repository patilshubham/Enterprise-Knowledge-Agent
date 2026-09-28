using EnterpriseKnowledgeAgent.Domain.Conversations;
using EnterpriseKnowledgeAgent.Shared.Agents;
using EnterpriseKnowledgeAgent.Shared.Chat;
using EnterpriseKnowledgeAgent.Shared.Errors;
using EnterpriseKnowledgeAgent.Shared.Retrieval;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Domain.Tests.Conversations;

/// <summary>Module CHAT: unit tests for the <see cref="Conversation"/> aggregate and its message lifecycle.</summary>
public sealed class ConversationTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static Conversation CreateConversation(
        KnowledgeScopeType scope = KnowledgeScopeType.AllReadyEnabled,
        IReadOnlyList<Guid>? selectedFileIds = null,
        string? systemPrompt = null) =>
        Conversation.Create(
            applicationId: Guid.NewGuid(),
            createdBy: "keycloak-sub-1",
            knowledgeScopeType: scope,
            selectedFileIds: selectedFileIds,
            generationDeployment: "gpt-4o",
            temperature: 0.2,
            retrievalMode: RetrievalMode.Semantic,
            retrievalCount: 8,
            minimumSimilarity: 0.3,
            maximumEvidenceTokens: 6_000,
            includeNeighbors: true,
            citationsRequired: true,
            systemPrompt: systemPrompt,
            now: Now);

    [Fact]
    public void Create_snapshots_settings_and_starts_with_the_default_title()
    {
        var conversation = CreateConversation();

        conversation.Id.Should().NotBeEmpty();
        conversation.Title.Should().Be(Conversation.DefaultTitle);
        conversation.CreatedBy.Should().Be("keycloak-sub-1");
        conversation.CreatedAt.Should().Be(Now);
        conversation.LastMessageAt.Should().Be(Now);
        conversation.IsDeleted.Should().BeFalse();
        conversation.KnowledgeScopeType.Should().Be(KnowledgeScopeType.AllReadyEnabled);
        conversation.SelectedFileIds.Should().BeEmpty();
        conversation.GenerationDeployment.Should().Be("gpt-4o");
        conversation.SystemPrompt.Should().BeNull();
        conversation.Messages.Should().BeEmpty();
    }

    [Fact]
    public void Create_with_selected_scope_requires_at_least_one_file()
    {
        var act = () => CreateConversation(KnowledgeScopeType.Selected, selectedFileIds: []);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_with_selected_scope_deduplicates_and_stores_the_file_ids()
    {
        var fileId = Guid.NewGuid();

        var conversation = CreateConversation(KnowledgeScopeType.Selected, selectedFileIds: [fileId, fileId]);

        conversation.KnowledgeScopeType.Should().Be(KnowledgeScopeType.Selected);
        conversation.SelectedFileIds.Should().ContainSingle().Which.Should().Be(fileId);
    }

    [Fact]
    public void StartUserMessage_derives_the_title_from_the_first_question_and_advances_activity()
    {
        var conversation = CreateConversation();
        var later = Now.AddMinutes(1);

        var message = conversation.StartUserMessage("How many vacation days do I get?", later);

        message.Role.Should().Be(MessageRole.User);
        message.Status.Should().Be(MessageStatus.Completed);
        message.Sequence.Should().Be(0);
        conversation.Title.Should().Be("How many vacation days do I get?");
        conversation.LastMessageAt.Should().Be(later);
        conversation.UpdatedAt.Should().Be(later);
    }

    [Fact]
    public void StartUserMessage_truncates_a_long_first_question_into_the_title()
    {
        var conversation = CreateConversation();
        var longQuestion = new string('a', Conversation.TitleSourceLength + 25);

        conversation.StartUserMessage(longQuestion, Now);

        conversation.Title.Should().HaveLength(Conversation.TitleSourceLength + 1);
        conversation.Title.Should().EndWith("…");
    }

    [Fact]
    public void StartUserMessage_does_not_change_the_title_after_the_first_message()
    {
        var conversation = CreateConversation();
        conversation.StartUserMessage("First question", Now);

        conversation.StartUserMessage("Second question", Now.AddMinutes(1));

        conversation.Title.Should().Be("First question");
        conversation.Messages.Should().HaveCount(2);
    }

    [Fact]
    public void StartAssistantMessage_creates_a_streaming_placeholder()
    {
        var conversation = CreateConversation();
        conversation.StartUserMessage("Question", Now);

        var assistant = conversation.StartAssistantMessage(Now);

        assistant.Role.Should().Be(MessageRole.Assistant);
        assistant.Status.Should().Be(MessageStatus.Streaming);
        assistant.Content.Should().BeEmpty();
        assistant.Sequence.Should().Be(1);
    }

    [Fact]
    public void Complete_finalizes_a_streaming_assistant_message_with_content_and_citations()
    {
        var conversation = CreateConversation();
        conversation.StartUserMessage("Question", Now);
        var assistant = conversation.StartAssistantMessage(Now);
        var completedAt = Now.AddSeconds(5);

        assistant.Complete("You get 20 days. [SOURCE-001]", ["SOURCE-001"], completedAt);

        assistant.Status.Should().Be(MessageStatus.Completed);
        assistant.Content.Should().Be("You get 20 days. [SOURCE-001]");
        assistant.CitedSourceIds.Should().ContainSingle().Which.Should().Be("SOURCE-001");
        assistant.CompletedAt.Should().Be(completedAt);
    }

    [Fact]
    public void SetNoResults_marks_the_message_as_no_results()
    {
        var conversation = CreateConversation();
        conversation.StartUserMessage("Question", Now);
        var assistant = conversation.StartAssistantMessage(Now);

        assistant.SetNoResults("No relevant information was found.", Now.AddSeconds(1));

        assistant.Status.Should().Be(MessageStatus.NoResults);
        assistant.Content.Should().Be("No relevant information was found.");
    }

    [Fact]
    public void Fail_records_the_category_and_user_message()
    {
        var conversation = CreateConversation();
        conversation.StartUserMessage("Question", Now);
        var assistant = conversation.StartAssistantMessage(Now);

        assistant.Fail(ErrorCategory.Throttled, "The assistant is busy right now.", Now.AddSeconds(1));

        assistant.Status.Should().Be(MessageStatus.Failed);
        assistant.FailureCategory.Should().Be(ErrorCategory.Throttled);
        assistant.FailureMessage.Should().Be("The assistant is busy right now.");
    }

    [Fact]
    public void Cancel_preserves_the_partial_content()
    {
        var conversation = CreateConversation();
        conversation.StartUserMessage("Question", Now);
        var assistant = conversation.StartAssistantMessage(Now);

        assistant.Cancel("Partial ans", Now.AddSeconds(1));

        assistant.Status.Should().Be(MessageStatus.Cancelled);
        assistant.Content.Should().Be("Partial ans");
    }

    [Fact]
    public void Finalizing_an_already_finalized_assistant_message_throws()
    {
        var conversation = CreateConversation();
        conversation.StartUserMessage("Question", Now);
        var assistant = conversation.StartAssistantMessage(Now);
        assistant.Complete("Done.", [], Now.AddSeconds(1));

        var act = () => assistant.Complete("Again.", [], Now.AddSeconds(2));

        act.Should().Throw<ConversationStateConflictException>();
    }

    [Fact]
    public void Rename_updates_and_trims_the_title()
    {
        var conversation = CreateConversation();

        conversation.Rename("  Renamed thread  ", Now.AddMinutes(1));

        conversation.Title.Should().Be("Renamed thread");
        conversation.UpdatedAt.Should().Be(Now.AddMinutes(1));
    }

    [Fact]
    public void Rename_rejects_a_blank_title()
    {
        var conversation = CreateConversation();

        var act = () => conversation.Rename("   ", Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SoftDelete_is_idempotent()
    {
        var conversation = CreateConversation();

        conversation.SoftDelete(Now.AddMinutes(1));
        conversation.SoftDelete(Now.AddMinutes(2));

        conversation.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void A_deleted_conversation_rejects_new_messages()
    {
        var conversation = CreateConversation();
        conversation.SoftDelete(Now);

        var act = () => conversation.StartUserMessage("Question", Now.AddMinutes(1));

        act.Should().Throw<ConversationStateConflictException>();
    }
}
