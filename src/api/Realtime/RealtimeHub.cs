using EnterpriseKnowledgeAgent.Application.Jobs;
using EnterpriseKnowledgeAgent.Shared.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace EnterpriseKnowledgeAgent.Api.Realtime;

/// <summary>
/// TASK-SIG-01: the realtime hub (ASDD §18.2). Open to any caller - there is
/// no login/authorization in this app. Event *publishing* is out of scope
/// here (TASK-SIG-03/04).
/// </summary>
public sealed class RealtimeHub(IAgentRunRepository agentRunRepository) : Hub
{
    public Task JoinApplication(Guid applicationId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Application(applicationId));

    public Task LeaveApplication(Guid applicationId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, RealtimeGroups.Application(applicationId));

    public async Task JoinRun(Guid runId)
    {
        var run = await agentRunRepository.GetAsync(runId, Context.ConnectionAborted);
        if (run is null)
        {
            throw new HubException("Run not found.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Run(runId));
    }

    public Task LeaveRun(Guid runId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, RealtimeGroups.Run(runId));

    public Task JoinOperations() =>
        Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.OperationsGroup);

    public Task LeaveOperations() =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, RealtimeGroups.OperationsGroup);
}
