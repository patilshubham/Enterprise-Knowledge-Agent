using System.Text.Json;
using EnterpriseKnowledgeAgent.Application.Agents;
using EnterpriseKnowledgeAgent.Application.AzureOpenAI;
using EnterpriseKnowledgeAgent.Application.Execution;
using EnterpriseKnowledgeAgent.Application.Jobs;
using EnterpriseKnowledgeAgent.Application.Realtime;
using EnterpriseKnowledgeAgent.Application.Tests.Processing;
using EnterpriseKnowledgeAgent.Domain.Execution;
using EnterpriseKnowledgeAgent.Shared.Agents;
using EnterpriseKnowledgeAgent.Shared.Jobs;
using EnterpriseKnowledgeAgent.Shared.Realtime;
using EnterpriseKnowledgeAgent.Shared.Retrieval;
using FluentAssertions;
using AgentEntity = EnterpriseKnowledgeAgent.Domain.Agents.Agent;

namespace EnterpriseKnowledgeAgent.Application.Tests.Execution;

/// <summary>TASK-RUN-01: unit tests for <see cref="AgentRunSubmissionService"/>'s validation/snapshot/idempotency logic.</summary>
public sealed class AgentRunSubmissionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private const string ChatDeployment = "gpt-4o";

    private static AgentEntity CreateAgent(Guid applicationId, AgentStatus status = AgentStatus.Active) =>
        BuildAgent(applicationId, status);

    private static AgentEntity BuildAgent(Guid applicationId, AgentStatus status)
    {
        var agent = AgentEntity.Create(
            applicationId,
            "Support Agent",
            "Answers support questions.",
            "You are a helpful assistant.",
            KnowledgeScopeType.AllReadyEnabled,
            selectedFileIds: null,
            ChatDeployment,
            temperature: 0.2,
            maximumOutputTokens: 800,
            topP: null,
            RetrievalMode.Hybrid,
            retrievalCount: 5,
            minimumSimilarity: 0.5,
            maximumEvidenceTokens: 4000,
            includeNeighbors: true,
            strictGrounding: true,
            citationsRequired: true,
            createdBy: "creator-sub",
            now: Now);

        if (status == AgentStatus.Disabled)
        {
            agent.Disable("creator-sub", Now);
        }
        else if (status == AgentStatus.Deleted)
        {
            agent.Delete("creator-sub", Now);
        }

        return agent;
    }

    private sealed class FakeAgentRepository : IAgentRepository
    {
        public readonly Dictionary<Guid, AgentEntity> Agents = [];

        public Task AddAsync(AgentEntity agent, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<AgentEntity?> GetAsync(Guid agentId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Agents.GetValueOrDefault(agentId));

        public Task<IReadOnlyList<AgentEntity>> ListAsync(Guid applicationId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AgentEntity>>([]);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeAgentRunRepository : IAgentRunRepository
    {
        public readonly Dictionary<Guid, AgentRun> Runs = [];

        public Task AddAsync(AgentRun run, CancellationToken cancellationToken = default)
        {
            Runs[run.Id] = run;
            return Task.CompletedTask;
        }

        public Task<AgentRun?> GetAsync(Guid runId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Runs.GetValueOrDefault(runId));

        public Task<AgentRun?> GetByIdempotencyKeyAsync(Guid agentId, string idempotencyKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(Runs.Values.FirstOrDefault(r => r.AgentId == agentId && r.IdempotencyKey == idempotencyKey));

        public Task<bool> TryClaimAsync(Guid runId, string workerId, DateTimeOffset now, TimeSpan leaseDuration, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> IsCancellationRequestedAsync(Guid runId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> HeartbeatAsync(Guid runId, string workerId, DateTimeOffset now, TimeSpan leaseExtension, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<AgentRun>> ListExpiredLeasesAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AgentRun>>([]);

        public Task<IReadOnlyList<AgentRun>> ListStaleUndeliveredAsync(DateTimeOffset createdBefore, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AgentRun>>([]);

        public Task<IReadOnlyList<AgentRun>> ListFailedAsync(Guid applicationId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AgentRun>>([]);

        public Task<IReadOnlyDictionary<JobStatus, int>> CountByStatusAsync(Guid? applicationId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<AgentRun>> ListAsync(
            Guid applicationId, JobStatus? status = null, Guid? agentId = null, string? createdBy = null, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeDeploymentRegistry : IDeploymentRegistry
    {
        public IReadOnlyList<DeploymentInfo> ListDeployments() =>
        [
            new DeploymentInfo(ChatDeployment, DeploymentPurpose.Chat, 128_000, 4_096, EmbeddingDimensions: null, SupportsTopP: true),
        ];
    }

    /// <summary>TASK-SIG-03/04: this suite exercises submission/snapshot logic only - realtime publishing is covered separately.</summary>
    private sealed class NoOpRealtimeEventPublisher : IRealtimeEventPublisher
    {
        public Task PublishAsync<TPayload>(string eventType, IReadOnlyCollection<string> groups, SignalREvent<TPayload> envelope, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private static (AgentRunSubmissionService Service, FakeAgentRepository Agents, FakeAgentRunRepository Runs) CreateService()
    {
        var agents = new FakeAgentRepository();
        var runs = new FakeAgentRunRepository();
        var jobCreationService = new JobCreationService(
            new FakeProcessingJobRepository(),
            runs,
            new FakeOutboxRepository(),
            new NoOpRealtimeEventPublisher(),
            new FakeTimeProvider(Now));
        var validator = new AgentParameterValidator(new FakeDeploymentRegistry());
        var service = new AgentRunSubmissionService(agents, runs, validator, jobCreationService);
        return (service, agents, runs);
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task SubmitAsync_creates_a_run_with_snapshot_fields()
    {
        var (service, agents, _) = CreateService();
        var applicationId = Guid.NewGuid();
        var agent = CreateAgent(applicationId);
        agents.Agents[agent.Id] = agent;

        var run = await service.SubmitAsync(
            applicationId, agent.Id, "requester-sub",
            new SubmitAgentRunRequest("Summarize the quarterly report", "Q3 summary", "key-1"));

        run.ApplicationId.Should().Be(applicationId);
        run.AgentId.Should().Be(agent.Id);
        run.UserRequest.Should().Be("Summarize the quarterly report");
        run.OutputTitle.Should().Be("Q3 summary");
        run.IdempotencyKey.Should().Be("key-1");
        run.AgentNameSnapshot.Should().Be("Support Agent");
        run.AgentPromptSnapshot.Should().Be(agent.Prompt);
        run.DeploymentSnapshot.Should().Be(ChatDeployment);
        run.KnowledgeScopeSnapshotJson.Should().BeNull();
        run.RequestedParametersJson.Should().Be(run.EffectiveParametersJson);
        run.RetrievalSettingsJson.Should().NotBeNullOrEmpty();
        run.OutboxMessageId.Should().NotBeNull();
    }

    [Fact]
    public async Task SubmitAsync_with_selected_knowledge_scope_serializes_file_ids()
    {
        var (service, agents, _) = CreateService();
        var applicationId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var agent = AgentEntity.Create(
            applicationId, "Scoped Agent", null, "Prompt", KnowledgeScopeType.Selected, [fileId],
            ChatDeployment, 0.2, 800, null, RetrievalMode.Hybrid, 5, 0.5, 4000, true, true, true, "creator", Now);
        agents.Agents[agent.Id] = agent;

        var run = await service.SubmitAsync(
            applicationId, agent.Id, "requester-sub",
            new SubmitAgentRunRequest("Question", null, "key-1"));

        run.KnowledgeScopeSnapshotJson.Should().NotBeNull();
        var ids = JsonSerializer.Deserialize<List<Guid>>(run.KnowledgeScopeSnapshotJson!);
        ids.Should().ContainSingle().Which.Should().Be(fileId);
    }

    [Fact]
    public async Task SubmitAsync_repeated_with_same_idempotency_key_returns_the_same_run()
    {
        var (service, agents, runs) = CreateService();
        var applicationId = Guid.NewGuid();
        var agent = CreateAgent(applicationId);
        agents.Agents[agent.Id] = agent;
        var request = new SubmitAgentRunRequest("Question", null, "same-key");

        var first = await service.SubmitAsync(applicationId, agent.Id, "requester-sub", request);
        var second = await service.SubmitAsync(applicationId, agent.Id, "requester-sub", request);

        second.Id.Should().Be(first.Id);
        runs.Runs.Count.Should().Be(1);
    }

    [Fact]
    public async Task SubmitAsync_for_unknown_agent_throws_not_found()
    {
        var (service, _, _) = CreateService();

        var act = () => service.SubmitAsync(
            Guid.NewGuid(), Guid.NewGuid(), "requester-sub", new SubmitAgentRunRequest("Question", null, "key-1"));

        await act.Should().ThrowAsync<AgentNotFoundException>();
    }

    [Fact]
    public async Task SubmitAsync_for_agent_in_another_application_throws_not_found()
    {
        var (service, agents, _) = CreateService();
        var agent = CreateAgent(Guid.NewGuid());
        agents.Agents[agent.Id] = agent;

        var act = () => service.SubmitAsync(
            Guid.NewGuid(), agent.Id, "requester-sub", new SubmitAgentRunRequest("Question", null, "key-1"));

        await act.Should().ThrowAsync<AgentNotFoundException>();
    }

    [Fact]
    public async Task SubmitAsync_for_disabled_agent_throws_validation_exception()
    {
        var (service, agents, _) = CreateService();
        var applicationId = Guid.NewGuid();
        var agent = CreateAgent(applicationId, AgentStatus.Disabled);
        agents.Agents[agent.Id] = agent;

        var act = () => service.SubmitAsync(
            applicationId, agent.Id, "requester-sub", new SubmitAgentRunRequest("Question", null, "key-1"));

        await act.Should().ThrowAsync<AgentValidationException>();
    }

    [Fact]
    public async Task SubmitAsync_for_deleted_agent_throws_validation_exception()
    {
        var (service, agents, _) = CreateService();
        var applicationId = Guid.NewGuid();
        var agent = CreateAgent(applicationId, AgentStatus.Deleted);
        agents.Agents[agent.Id] = agent;

        var act = () => service.SubmitAsync(
            applicationId, agent.Id, "requester-sub", new SubmitAgentRunRequest("Question", null, "key-1"));

        await act.Should().ThrowAsync<AgentValidationException>();
    }
}
