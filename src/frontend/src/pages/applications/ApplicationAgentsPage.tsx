import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { createColumnHelper } from '@tanstack/react-table';
import { format } from 'date-fns';
import { BanIcon, Bot, CheckIcon, PlusIcon, TrashIcon } from 'lucide-react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { toast } from 'sonner';
import {
  agentsQueryKeys,
  createAgent,
  deleteAgent,
  disableAgent,
  enableAgent,
  listAgents,
  type AgentResponse,
} from '@/api/agents';
import { chatDeployments, deploymentsQueryKeys, listDeployments } from '@/api/deployments';
import { ApiError } from '@/api/problemDetails';
import { useCapabilities } from '@/app/authz/useCapabilities';
import {
  AgentCreateDialog,
  type AgentCreateFormValues,
} from '@/components/agents/AgentCreateDialog';
import { STATUS_KIND_BY_AGENT_STATUS } from '@/components/agents/agentStatus';
import { ConfirmDialog } from '@/components/dialogs/ConfirmDialog';
import { PageHeader } from '@/components/patterns/PageHeader';
import { Button } from '@/components/ui/button';
import { DataTable, type DataTableColumnDef } from '@/components/data-table/DataTable';
import type { DataTableRowAction } from '@/components/data-table/DataTableRowActions';
import type { DataTableFeatures } from '@/components/data-table/table-features';
import { StatusBadge } from '@/components/status/StatusBadge';

type PendingAction = { type: 'enable' | 'disable' | 'delete'; agent: AgentResponse } | null;

function errorMessage(error: unknown, fallback: string): string {
  return error instanceof ApiError ? (error.problem.detail ?? error.problem.title) : fallback;
}

/** A conservative starter prompt - the user is routed straight to the full editor to refine it (TASK-AGT-04). */
const DEFAULT_STARTER_PROMPT =
  'You are a helpful assistant. Answer questions using only the provided knowledge base evidence, and cite your sources.';

/**
 * TASK-AGT-02/04: agents list (`/applications/:applicationId/agents`).
 * Viewable by any application member (server-enforced `CanViewApplication`);
 * create/enable/disable/delete are further gated to `canManageAgents`
 * (SuperAdmin/ApplicationAdmin). Creating an agent only asks for a name and
 * description here - the rest of the configuration gets sane defaults and is
 * refined in the full editor the user is routed to immediately after.
 */
export default function ApplicationAgentsPage() {
  const { applicationId } = useParams<{ applicationId: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const capabilities = useCapabilities(applicationId);

  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [pendingAction, setPendingAction] = useState<PendingAction>(null);

  const agentsQuery = useQuery({
    queryKey: agentsQueryKeys.list(applicationId ?? ''),
    queryFn: () => listAgents(applicationId ?? ''),
    enabled: Boolean(applicationId),
  });

  const deploymentsQuery = useQuery({
    queryKey: deploymentsQueryKeys.list(),
    queryFn: listDeployments,
  });

  const defaultChatDeployment = useMemo(() => {
    const deployments = deploymentsQuery.data ?? [];
    return chatDeployments(deployments)[0] ?? null;
  }, [deploymentsQuery.data]);

  function invalidateList() {
    return queryClient.invalidateQueries({ queryKey: agentsQueryKeys.list(applicationId ?? '') });
  }

  const createMutation = useMutation({
    mutationFn: (values: AgentCreateFormValues) =>
      createAgent(applicationId ?? '', {
        name: values.name,
        description: values.description || null,
        prompt: DEFAULT_STARTER_PROMPT,
        knowledgeScopeType: 'AllReadyEnabled',
        selectedFileIds: null,
        generationDeployment: defaultChatDeployment?.name ?? '',
        temperature: 0.7,
        maximumOutputTokens: Math.min(2048, defaultChatDeployment?.maxOutputTokens ?? 2048),
        topP: null,
        retrievalMode: 'Hybrid',
        retrievalCount: 8,
        // Applied to the min-max normalized *fused* hybrid score (semantic +
        // keyword), not a raw cosine similarity - the top candidate rarely
        // exceeds ~0.6 unless it tops both lists, so a high floor (e.g. 0.7)
        // combined with strict grounding makes every run fail with
        // "no evidence matched". 0.35 keeps strong matches while filtering the
        // weak tail. Tune per agent in the editor.
        minimumSimilarity: 0.35,
        maximumEvidenceTokens: 4000,
        includeNeighbors: true,
        strictGrounding: true,
        citationsRequired: true,
      }),
    onSuccess: async (created) => {
      await invalidateList();
      setIsCreateOpen(false);
      toast.success('Agent created - configure it below');
      navigate(`/applications/${applicationId}/agents/${created.id}`);
    },
    onError: (error) => toast.error(errorMessage(error, 'Failed to create agent')),
  });

  const enableMutation = useMutation({
    mutationFn: (agentId: string) => enableAgent(applicationId ?? '', agentId),
    onSuccess: async () => {
      await invalidateList();
      setPendingAction(null);
      toast.success('Agent enabled');
    },
    onError: (error) => toast.error(errorMessage(error, 'Failed to enable agent')),
  });

  const disableMutation = useMutation({
    mutationFn: (agentId: string) => disableAgent(applicationId ?? '', agentId),
    onSuccess: async () => {
      await invalidateList();
      setPendingAction(null);
      toast.success('Agent disabled');
    },
    onError: (error) => toast.error(errorMessage(error, 'Failed to disable agent')),
  });

  const deleteMutation = useMutation({
    mutationFn: (agentId: string) => deleteAgent(applicationId ?? '', agentId),
    onSuccess: async () => {
      await invalidateList();
      setPendingAction(null);
      toast.success('Agent deleted');
    },
    onError: (error) => toast.error(errorMessage(error, 'Failed to delete agent')),
  });

  const columnHelper = useMemo(() => createColumnHelper<DataTableFeatures, AgentResponse>(), []);

  const columns: DataTableColumnDef<AgentResponse>[] = useMemo(
    () => [
      columnHelper.accessor('name', {
        header: 'Name',
        cell: (info) => (
          <Link
            to={`/applications/${applicationId}/agents/${info.row.original.id}`}
            className="font-medium text-text-primary hover:underline"
            onClick={(event) => event.stopPropagation()}
          >
            {info.getValue()}
          </Link>
        ),
      }),
      columnHelper.accessor('status', {
        header: 'Status',
        cell: (info) => {
          const status = info.row.original.status;
          return <StatusBadge status={STATUS_KIND_BY_AGENT_STATUS[status]} label={status} />;
        },
      }),
      columnHelper.accessor('generationDeployment', {
        header: 'Deployment',
      }),
      columnHelper.accessor('knowledgeScopeType', {
        header: 'Knowledge scope',
        cell: (info) =>
          info.getValue() === 'AllReadyEnabled' ? 'All ready + enabled' : 'Selected',
      }),
      columnHelper.accessor('updatedAt', {
        header: 'Updated',
        cell: (info) => {
          const value = info.getValue() ?? info.row.original.createdAt;
          return format(new Date(value), 'PP');
        },
      }),
    ],
    [columnHelper, applicationId],
  );

  function rowActions(agent: AgentResponse): DataTableRowAction[] {
    if (!capabilities.canManageAgents || agent.status === 'Deleted') {
      return [];
    }

    const actions: DataTableRowAction[] = [];
    if (agent.status === 'Active') {
      actions.push({
        id: 'disable',
        label: 'Disable',
        icon: <BanIcon />,
        onSelect: () => setPendingAction({ type: 'disable', agent }),
      });
    }
    if (agent.status === 'Disabled') {
      actions.push({
        id: 'enable',
        label: 'Enable',
        icon: <CheckIcon />,
        onSelect: () => setPendingAction({ type: 'enable', agent }),
      });
    }
    actions.push({
      id: 'delete',
      label: 'Delete',
      icon: <TrashIcon />,
      destructive: true,
      onSelect: () => setPendingAction({ type: 'delete', agent }),
    });
    return actions;
  }

  const isMutating =
    enableMutation.isPending || disableMutation.isPending || deleteMutation.isPending;

  return (
    <div aria-labelledby="agents-heading" className="mx-auto max-w-6xl space-y-6">
      <PageHeader
        eyebrow="Application"
        icon={Bot}
        title="Agents"
        description="Reusable, configured assistants that retrieve from your knowledge and generate grounded outputs."
        actions={
          capabilities.canManageAgents && (
            <Button onClick={() => setIsCreateOpen(true)}>
              <PlusIcon />
              Create agent
            </Button>
          )
        }
      />

      {agentsQuery.isError ? (
        <p role="alert" className="text-sm text-danger">
          {errorMessage(agentsQuery.error, 'Failed to load agents')}
        </p>
      ) : (
        <DataTable
          columns={columns}
          data={agentsQuery.data ?? []}
          isLoading={agentsQuery.isLoading}
          getRowId={(row) => row.id}
          searchPlaceholder="Search agents..."
          emptyState={
            capabilities.canManageAgents
              ? 'No agents yet - create one to get started.'
              : 'No agents to show.'
          }
          rowActions={rowActions}
        />
      )}

      <AgentCreateDialog
        open={isCreateOpen}
        onOpenChange={setIsCreateOpen}
        onSubmit={(values) => createMutation.mutate(values)}
        isPending={createMutation.isPending}
        blockedReason={
          !deploymentsQuery.isLoading && !defaultChatDeployment
            ? 'No chat deployment is configured - an administrator must configure one before agents can be created.'
            : null
        }
      />

      <ConfirmDialog
        open={pendingAction !== null}
        onOpenChange={(open) => !open && setPendingAction(null)}
        title={
          pendingAction?.type === 'disable'
            ? 'Disable agent?'
            : pendingAction?.type === 'enable'
              ? 'Enable agent?'
              : 'Delete agent?'
        }
        description={
          pendingAction?.type === 'disable'
            ? `"${pendingAction.agent.name}" can no longer be run until re-enabled.`
            : pendingAction?.type === 'enable'
              ? `"${pendingAction.agent.name}" becomes runnable again.`
              : `"${pendingAction?.agent.name}" will be soft-deleted. Historical outputs remain viewable, but it can no longer be run.`
        }
        confirmLabel={
          pendingAction?.type === 'disable'
            ? 'Disable'
            : pendingAction?.type === 'enable'
              ? 'Enable'
              : 'Delete'
        }
        destructive={pendingAction?.type === 'delete' || pendingAction?.type === 'disable'}
        isPending={isMutating}
        onConfirm={() => {
          if (!pendingAction) {
            return;
          }
          const { type, agent } = pendingAction;
          if (type === 'disable') {
            disableMutation.mutate(agent.id);
          } else if (type === 'enable') {
            enableMutation.mutate(agent.id);
          } else {
            deleteMutation.mutate(agent.id);
          }
        }}
      />
    </div>
  );
}
