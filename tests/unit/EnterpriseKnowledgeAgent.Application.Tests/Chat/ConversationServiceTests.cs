using EnterpriseKnowledgeAgent.Application.AzureOpenAI;
using EnterpriseKnowledgeAgent.Application.Chat;
using EnterpriseKnowledgeAgent.Domain.Conversations;
using EnterpriseKnowledgeAgent.Shared.Agents;
using EnterpriseKnowledgeAgent.Shared.Retrieval;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace EnterpriseKnowledgeAgent.Application.Tests.Chat;

/// <summary>Module CHAT: unit tests for <see cref="ConversationService"/>'s config-free defaults and advanced overrides.</summary>
public sealed class ConversationServiceTests
{
    private const string DefaultChatDeployment = "gpt-4o";
    private const string SecondaryChatDeployment = "gpt-4o-mini";
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static (ConversationService Service, FakeConversationRepository Repository) CreateService()
    {
        var repository = new FakeConversationRepository();
        var service = new ConversationService(
            repository,
            new FakeDeploymentRegistry(),
            Options.Create(new AoiOptions { ChatDeployment = DefaultChatDeployment }),
            new FakeTimeProvider(Now));
        return (service, repository);
    }

    [Fact]
    public async Task CreateAsync_with_no_advanced_config_applies_platform_defaults()
    {
        var (service, repository) = CreateService();

        var detail = await service.CreateAsync(Guid.NewGuid(), "keycloak-sub-1", new CreateConversationRequest());

        detail.Title.Should().Be(Conversation.DefaultTitle);
        detail.Settings.KnowledgeScope.Should().Be(KnowledgeScopeType.AllReadyEnabled.ToString());
        detail.Settings.GenerationDeployment.Should().Be(DefaultChatDeployment);
        detail.Settings.Temperature.Should().Be(ChatDefaults.Temperature);
        detail.Settings.RetrievalMode.Should().Be(RetrievalMode.Semantic.ToString());
        detail.Settings.HasCustomSystemPrompt.Should().BeFalse();
        repository.Saved.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateAsync_honors_advanced_overrides()
    {
        var (service, _) = CreateService();
        var fileId = Guid.NewGuid();
        var advanced = new ChatAdvancedConfigRequest(
            KnowledgeScope: "Selected",
            SelectedFileIds: [fileId],
            GenerationDeployment: SecondaryChatDeployment,
            Temperature: 0.9,
            RetrievalMode: "Hybrid",
            SystemPrompt: "You are a compliance expert.");

        var detail = await service.CreateAsync(Guid.NewGuid(), "keycloak-sub-1", new CreateConversationRequest(advanced));

        detail.Settings.KnowledgeScope.Should().Be(KnowledgeScopeType.Selected.ToString());
        detail.Settings.SelectedFileIds.Should().ContainSingle().Which.Should().Be(fileId);
        detail.Settings.GenerationDeployment.Should().Be(SecondaryChatDeployment);
        detail.Settings.Temperature.Should().Be(0.9);
        detail.Settings.RetrievalMode.Should().Be(RetrievalMode.Hybrid.ToString());
        detail.Settings.HasCustomSystemPrompt.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_rejects_a_deployment_that_is_not_a_configured_chat_deployment()
    {
        var (service, _) = CreateService();
        var advanced = new ChatAdvancedConfigRequest(GenerationDeployment: "text-embedding-3-large");

        var act = () => service.CreateAsync(Guid.NewGuid(), "keycloak-sub-1", new CreateConversationRequest(advanced));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateAsync_rejects_selected_scope_without_files()
    {
        var (service, _) = CreateService();
        var advanced = new ChatAdvancedConfigRequest(KnowledgeScope: "Selected", SelectedFileIds: []);

        var act = () => service.CreateAsync(Guid.NewGuid(), "keycloak-sub-1", new CreateConversationRequest(advanced));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RenameAsync_updates_the_title()
    {
        var (service, _) = CreateService();
        var conversation = Conversation.Create(
            Guid.NewGuid(), "keycloak-sub-1", KnowledgeScopeType.AllReadyEnabled, null, DefaultChatDeployment,
            ChatDefaults.Temperature, RetrievalMode.Semantic, ChatDefaults.RetrievalCount, ChatDefaults.MinimumSimilarity,
            ChatDefaults.MaximumEvidenceTokens, true, true, null, Now);

        var summary = await service.RenameAsync(conversation, "Renamed");

        summary.Title.Should().Be("Renamed");
        conversation.Title.Should().Be("Renamed");
    }

    [Fact]
    public async Task DeleteAsync_soft_deletes_the_conversation()
    {
        var (service, _) = CreateService();
        var conversation = Conversation.Create(
            Guid.NewGuid(), "keycloak-sub-1", KnowledgeScopeType.AllReadyEnabled, null, DefaultChatDeployment,
            ChatDefaults.Temperature, RetrievalMode.Semantic, ChatDefaults.RetrievalCount, ChatDefaults.MinimumSimilarity,
            ChatDefaults.MaximumEvidenceTokens, true, true, null, Now);

        await service.DeleteAsync(conversation);

        conversation.IsDeleted.Should().BeTrue();
    }

    // --- Fakes ---

    private sealed class FakeConversationRepository : IConversationRepository
    {
        public List<Conversation> Saved { get; } = [];

        public Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default)
        {
            Saved.Add(conversation);
            return Task.CompletedTask;
        }

        public Task<Conversation?> GetAsync(Guid applicationId, Guid conversationId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Conversation?>(Saved.FirstOrDefault(c => c.Id == conversationId));

        public Task<IReadOnlyList<ConversationListItem>> ListAsync(Guid applicationId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ConversationListItem>>([]);

        public Task<IReadOnlyList<ConversationMessageEvidence>> GetEvidenceAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ConversationMessageEvidence>>([]);

        public Task AddEvidenceAsync(IReadOnlyList<ConversationMessageEvidence> evidence, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeDeploymentRegistry : IDeploymentRegistry
    {
        public IReadOnlyList<DeploymentInfo> ListDeployments() =>
        [
            new DeploymentInfo(DefaultChatDeployment, DeploymentPurpose.Chat, 128_000, 4_096, EmbeddingDimensions: null, SupportsTopP: true),
            new DeploymentInfo(SecondaryChatDeployment, DeploymentPurpose.Chat, 128_000, 4_096, EmbeddingDimensions: null, SupportsTopP: true),
            new DeploymentInfo("text-embedding-3-large", DeploymentPurpose.Embedding, 8_191, 0, EmbeddingDimensions: 3_072, SupportsTopP: false),
        ];
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
