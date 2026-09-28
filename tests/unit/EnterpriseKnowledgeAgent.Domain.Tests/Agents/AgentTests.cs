using EnterpriseKnowledgeAgent.Domain.Agents;
using EnterpriseKnowledgeAgent.Shared.Agents;
using EnterpriseKnowledgeAgent.Shared.Retrieval;
using FluentAssertions;
using AgentEntity = EnterpriseKnowledgeAgent.Domain.Agents.Agent;

namespace EnterpriseKnowledgeAgent.Domain.Tests.Agents;

/// <summary>TASK-AGT-01: unit tests for the Agent aggregate's guarded state-transition + validation rules.</summary>
public sealed class AgentTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid ApplicationId = Guid.NewGuid();

    private static AgentEntity CreateValid(
        KnowledgeScopeType scopeType = KnowledgeScopeType.AllReadyEnabled,
        IReadOnlyList<Guid>? selectedFileIds = null,
        double? topP = null) =>
        AgentEntity.Create(
            ApplicationId,
            "Support Agent",
            "Answers support questions",
            "You are a helpful support agent.",
            scopeType,
            selectedFileIds,
            "gpt-4o",
            0.7,
            2048,
            topP,
            RetrievalMode.Hybrid,
            10,
            0.5,
            8000,
            includeNeighbors: true,
            strictGrounding: true,
            citationsRequired: true,
            "keycloak-sub-1",
            Now);

    [Fact]
    public void Create_sets_active_status_and_provided_fields()
    {
        var agent = CreateValid();

        agent.ApplicationId.Should().Be(ApplicationId);
        agent.Name.Should().Be("Support Agent");
        agent.Description.Should().Be("Answers support questions");
        agent.Prompt.Should().Be("You are a helpful support agent.");
        agent.Status.Should().Be(AgentStatus.Active);
        agent.KnowledgeScopeType.Should().Be(KnowledgeScopeType.AllReadyEnabled);
        agent.GenerationDeployment.Should().Be("gpt-4o");
        agent.Temperature.Should().Be(0.7);
        agent.MaximumOutputTokens.Should().Be(2048);
        agent.RetrievalMode.Should().Be(RetrievalMode.Hybrid);
        agent.RetrievalCount.Should().Be(10);
        agent.MinimumSimilarity.Should().Be(0.5);
        agent.MaximumEvidenceTokens.Should().Be(8000);
        agent.IncludeNeighbors.Should().BeTrue();
        agent.StrictGrounding.Should().BeTrue();
        agent.CitationsRequired.Should().BeTrue();
        agent.CreatedBy.Should().Be("keycloak-sub-1");
        agent.CreatedAt.Should().Be(Now);
        agent.UpdatedBy.Should().BeNull();
        agent.UpdatedAt.Should().BeNull();
        agent.IsReadOnly.Should().BeFalse();
        agent.SelectedFileIds.Should().BeEmpty();
    }

    [Fact]
    public void Create_trims_name_description_and_prompt()
    {
        var agent = AgentEntity.Create(
            ApplicationId, "  Support  ", "  desc  ", "  prompt  ", KnowledgeScopeType.AllReadyEnabled, null,
            "gpt-4o", 0.5, 100, null, RetrievalMode.Semantic, 5, 0.3, 1000, false, false, false, "sub", Now);

        agent.Name.Should().Be("Support");
        agent.Description.Should().Be("desc");
        agent.Prompt.Should().Be("prompt");
    }

    [Fact]
    public void Create_normalizes_blank_description_to_null()
    {
        var agent = AgentEntity.Create(
            ApplicationId, "Support", "   ", "prompt", KnowledgeScopeType.AllReadyEnabled, null,
            "gpt-4o", 0.5, 100, null, RetrievalMode.Semantic, 5, 0.3, 1000, false, false, false, "sub", Now);

        agent.Description.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_name(string? name)
    {
        var act = () => AgentEntity.Create(
            ApplicationId, name!, null, "prompt", KnowledgeScopeType.AllReadyEnabled, null,
            "gpt-4o", 0.5, 100, null, RetrievalMode.Semantic, 5, 0.3, 1000, false, false, false, "sub", Now);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_prompt(string? prompt)
    {
        var act = () => AgentEntity.Create(
            ApplicationId, "Support", null, prompt!, KnowledgeScopeType.AllReadyEnabled, null,
            "gpt-4o", 0.5, 100, null, RetrievalMode.Semantic, 5, 0.3, 1000, false, false, false, "sub", Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rejects_name_over_max_length()
    {
        var tooLong = new string('a', AgentEntity.MaxNameLength + 1);

        var act = () => AgentEntity.Create(
            ApplicationId, tooLong, null, "prompt", KnowledgeScopeType.AllReadyEnabled, null,
            "gpt-4o", 0.5, 100, null, RetrievalMode.Semantic, 5, 0.3, 1000, false, false, false, "sub", Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rejects_prompt_over_max_length()
    {
        var tooLong = new string('a', AgentEntity.MaxPromptLength + 1);

        var act = () => AgentEntity.Create(
            ApplicationId, "Support", null, tooLong, KnowledgeScopeType.AllReadyEnabled, null,
            "gpt-4o", 0.5, 100, null, RetrievalMode.Semantic, 5, 0.3, 1000, false, false, false, "sub", Now);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(2.1)]
    public void Create_rejects_temperature_out_of_range(double temperature)
    {
        var act = () => AgentEntity.Create(
            ApplicationId, "Support", null, "prompt", KnowledgeScopeType.AllReadyEnabled, null,
            "gpt-4o", temperature, 100, null, RetrievalMode.Semantic, 5, 0.3, 1000, false, false, false, "sub", Now);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Create_rejects_topP_out_of_range(double topP)
    {
        var act = () => AgentEntity.Create(
            ApplicationId, "Support", null, "prompt", KnowledgeScopeType.AllReadyEnabled, null,
            "gpt-4o", 0.5, 100, topP, RetrievalMode.Semantic, 5, 0.3, 1000, false, false, false, "sub", Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_allows_null_topP()
    {
        var agent = CreateValid(topP: null);

        agent.TopP.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_rejects_non_positive_maximum_output_tokens(int maxOutput)
    {
        var act = () => AgentEntity.Create(
            ApplicationId, "Support", null, "prompt", KnowledgeScopeType.AllReadyEnabled, null,
            "gpt-4o", 0.5, maxOutput, null, RetrievalMode.Semantic, 5, 0.3, 1000, false, false, false, "sub", Now);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public void Create_rejects_retrieval_count_out_of_range(int retrievalCount)
    {
        var act = () => AgentEntity.Create(
            ApplicationId, "Support", null, "prompt", KnowledgeScopeType.AllReadyEnabled, null,
            "gpt-4o", 0.5, 100, null, RetrievalMode.Semantic, retrievalCount, 0.3, 1000, false, false, false, "sub", Now);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Create_rejects_minimum_similarity_out_of_range(double minimumSimilarity)
    {
        var act = () => AgentEntity.Create(
            ApplicationId, "Support", null, "prompt", KnowledgeScopeType.AllReadyEnabled, null,
            "gpt-4o", 0.5, 100, null, RetrievalMode.Semantic, 5, minimumSimilarity, 1000, false, false, false, "sub", Now);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_rejects_non_positive_maximum_evidence_tokens(int maxEvidenceTokens)
    {
        var act = () => AgentEntity.Create(
            ApplicationId, "Support", null, "prompt", KnowledgeScopeType.AllReadyEnabled, null,
            "gpt-4o", 0.5, 100, null, RetrievalMode.Semantic, 5, 0.3, maxEvidenceTokens, false, false, false, "sub", Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_with_selected_scope_and_no_files_throws()
    {
        var act = () => CreateValid(KnowledgeScopeType.Selected, selectedFileIds: []);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_with_selected_scope_stores_distinct_file_ids()
    {
        var fileId1 = Guid.NewGuid();
        var fileId2 = Guid.NewGuid();

        var agent = CreateValid(KnowledgeScopeType.Selected, [fileId1, fileId2, fileId1]);

        agent.KnowledgeScopeType.Should().Be(KnowledgeScopeType.Selected);
        agent.SelectedFileIds.Should().BeEquivalentTo([fileId1, fileId2]);
    }

    [Fact]
    public void Update_overwrites_configuration_and_sets_audit_fields()
    {
        var agent = CreateValid();

        agent.Update(
            "Renamed", "new desc", "new prompt", KnowledgeScopeType.AllReadyEnabled, null,
            "gpt-4o-mini", 1.0, 512, 0.9, RetrievalMode.Semantic, 3, 0.2, 2000, false, false, false,
            "keycloak-sub-2", Now.AddHours(1));

        agent.Name.Should().Be("Renamed");
        agent.Description.Should().Be("new desc");
        agent.Prompt.Should().Be("new prompt");
        agent.GenerationDeployment.Should().Be("gpt-4o-mini");
        agent.Temperature.Should().Be(1.0);
        agent.MaximumOutputTokens.Should().Be(512);
        agent.TopP.Should().Be(0.9);
        agent.RetrievalMode.Should().Be(RetrievalMode.Semantic);
        agent.RetrievalCount.Should().Be(3);
        agent.MinimumSimilarity.Should().Be(0.2);
        agent.MaximumEvidenceTokens.Should().Be(2000);
        agent.IncludeNeighbors.Should().BeFalse();
        agent.StrictGrounding.Should().BeFalse();
        agent.CitationsRequired.Should().BeFalse();
        agent.UpdatedBy.Should().Be("keycloak-sub-2");
        agent.UpdatedAt.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void Update_after_delete_throws_conflict()
    {
        var agent = CreateValid();
        agent.Delete("sub", Now);

        var act = () => agent.Update(
            "x", null, "prompt", KnowledgeScopeType.AllReadyEnabled, null, "gpt-4o", 0.5, 100, null,
            RetrievalMode.Semantic, 5, 0.3, 1000, false, false, false, "sub", Now);

        act.Should().Throw<AgentStateConflictException>();
    }

    [Fact]
    public void Disable_from_active_moves_to_disabled()
    {
        var agent = CreateValid();

        agent.Disable("sub", Now.AddHours(1));

        agent.Status.Should().Be(AgentStatus.Disabled);
        agent.UpdatedBy.Should().Be("sub");
        agent.UpdatedAt.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void Disable_from_disabled_throws_conflict()
    {
        var agent = CreateValid();
        agent.Disable("sub", Now);

        var act = () => agent.Disable("sub", Now);

        act.Should().Throw<AgentStateConflictException>();
    }

    [Fact]
    public void Enable_from_disabled_moves_to_active()
    {
        var agent = CreateValid();
        agent.Disable("sub", Now);

        agent.Enable("sub", Now.AddHours(1));

        agent.Status.Should().Be(AgentStatus.Active);
        agent.UpdatedAt.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void Enable_from_active_throws_conflict()
    {
        var agent = CreateValid();

        var act = () => agent.Enable("sub", Now);

        act.Should().Throw<AgentStateConflictException>();
    }

    [Fact]
    public void Delete_is_idempotent()
    {
        var agent = CreateValid();

        agent.Delete("sub", Now);
        var act = () => agent.Delete("sub", Now.AddHours(1));

        act.Should().NotThrow();
        agent.Status.Should().Be(AgentStatus.Deleted);
    }

    [Fact]
    public void Delete_sets_readonly()
    {
        var agent = CreateValid();

        agent.Delete("sub", Now);

        agent.IsReadOnly.Should().BeTrue();
        agent.Status.Should().Be(AgentStatus.Deleted);
    }

    [Fact]
    public void Enable_after_delete_throws_conflict()
    {
        var agent = CreateValid();
        agent.Delete("sub", Now);

        var act = () => agent.Enable("sub", Now);

        act.Should().Throw<AgentStateConflictException>();
    }
}
