using EnterpriseKnowledgeAgent.Application.Agents;
using EnterpriseKnowledgeAgent.Shared.Agents;
using EnterpriseKnowledgeAgent.Shared.Retrieval;
using FluentAssertions;
using AgentEntity = EnterpriseKnowledgeAgent.Domain.Agents.Agent;

namespace EnterpriseKnowledgeAgent.Application.Tests.Agents;

/// <summary>TASK-AGT-05: unit tests for <see cref="AgentKnowledgeScopeResolver"/>'s translation to <c>RetrievalRequest.SelectedFileIds</c> shape.</summary>
public sealed class AgentKnowledgeScopeResolverTests
{
    private static AgentEntity CreateAgent(KnowledgeScopeType scopeType, IReadOnlyList<Guid>? selectedFileIds) =>
        AgentEntity.Create(
            Guid.NewGuid(), "Agent", null, "prompt", scopeType, selectedFileIds,
            "gpt-4o", 0.5, 100, null, RetrievalMode.Semantic, 5, 0.3, 1000, false, false, false, "sub", DateTimeOffset.UtcNow);

    [Fact]
    public void ResolveSelectedFileIds_returns_null_for_all_ready_enabled_scope()
    {
        var agent = CreateAgent(KnowledgeScopeType.AllReadyEnabled, null);

        var resolved = AgentKnowledgeScopeResolver.ResolveSelectedFileIds(agent);

        resolved.Should().BeNull();
    }

    [Fact]
    public void ResolveSelectedFileIds_returns_agent_file_ids_for_selected_scope()
    {
        var fileId1 = Guid.NewGuid();
        var fileId2 = Guid.NewGuid();
        var agent = CreateAgent(KnowledgeScopeType.Selected, [fileId1, fileId2]);

        var resolved = AgentKnowledgeScopeResolver.ResolveSelectedFileIds(agent);

        resolved.Should().BeEquivalentTo([fileId1, fileId2]);
    }
}
