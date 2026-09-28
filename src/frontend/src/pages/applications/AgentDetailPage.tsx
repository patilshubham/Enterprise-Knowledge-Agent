import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Bot } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { useNavigate, useParams } from 'react-router-dom';
import { toast } from 'sonner';
import { z } from 'zod';
import {
  agentsQueryKeys,
  deleteAgent,
  disableAgent,
  enableAgent,
  getAgent,
  updateAgent,
  type AgentFormRequest,
  type AgentResponse,
} from '@/api/agents';
import {
  chatDeployments,
  deploymentsQueryKeys,
  listDeployments,
  type DeploymentInfo,
} from '@/api/deployments';
import { ApiError } from '@/api/problemDetails';
import { submitRun } from '@/api/runs';
import { useCapabilities } from '@/app/authz/useCapabilities';
import { STATUS_KIND_BY_AGENT_STATUS } from '@/components/agents/agentStatus';
import { KnowledgeScopeField } from '@/components/agents/KnowledgeScopeField';
import {
  GENERATION_PRESETS,
  GENERATION_PRESET_TEMPERATURES,
  presetForTemperature,
} from '@/components/agents/generationPresets';
import { ConfirmDialog } from '@/components/dialogs/ConfirmDialog';
import { CodeEditor } from '@/components/editor/CodeEditor';
import { PageHeader } from '@/components/patterns/PageHeader';
import { RunAgentDialog } from '@/components/runs/RunAgentDialog';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form';
import { Input } from '@/components/ui/input';
import { ResizableHandle, ResizablePanel, ResizablePanelGroup } from '@/components/ui/resizable';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusBadge } from '@/components/status/StatusBadge';
import { Textarea } from '@/components/ui/textarea';
import { estimateTokenCount } from '@/lib/tokenEstimate';
import { cn } from '@/lib/utils';

function errorMessage(error: unknown, fallback: string): string {
  return error instanceof ApiError ? (error.problem.detail ?? error.problem.title) : fallback;
}

const agentFormSchema = z.object({
  name: z.string().trim().min(1, 'Name is required').max(200, 'Name is too long'),
  description: z.string().trim().max(2000, 'Description is too long').optional(),
  prompt: z.string().trim().min(1, 'Prompt is required').max(100_000, 'Prompt is too long'),
  generationDeployment: z.string().min(1, 'Select a deployment'),
  temperature: z.number().min(0, 'Must be at least 0').max(2, 'Must be at most 2'),
  maximumOutputTokens: z.number().int().min(1, 'Must be greater than 0'),
  topP: z.number().min(0).max(1).nullable(),
  retrievalMode: z.enum(['Semantic', 'Hybrid']),
  retrievalCount: z.number().int().min(1, 'Must be at least 1').max(50, 'Must be at most 50'),
  minimumSimilarity: z.number().min(0).max(1),
  maximumEvidenceTokens: z.number().int().min(1, 'Must be greater than 0'),
  includeNeighbors: z.boolean(),
  strictGrounding: z.boolean(),
  citationsRequired: z.boolean(),
  knowledgeScopeType: z.enum(['AllReadyEnabled', 'Selected']),
  selectedFileIds: z.array(z.string()),
});

type AgentFormValues = z.infer<typeof agentFormSchema>;

/** TASK-AGT-04: `/applications/:applicationId/agents/:agentId` - the full agent editor. */
export default function AgentDetailPage() {
  const { applicationId, agentId } = useParams<{ applicationId: string; agentId: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const capabilities = useCapabilities(applicationId);

  const [pendingAction, setPendingAction] = useState<'disable' | 'enable' | 'delete' | null>(null);
  const [isRunDialogOpen, setIsRunDialogOpen] = useState(false);

  const agentQuery = useQuery({
    queryKey: agentsQueryKeys.detail(applicationId ?? '', agentId ?? ''),
    queryFn: () => getAgent(applicationId ?? '', agentId ?? ''),
    enabled: Boolean(applicationId) && Boolean(agentId),
  });

  const deploymentsQuery = useQuery({
    queryKey: deploymentsQueryKeys.list(),
    queryFn: listDeployments,
  });

  const deployments = useMemo(
    () => chatDeployments(deploymentsQuery.data ?? []),
    [deploymentsQuery.data],
  );

  function invalidate() {
    return Promise.all([
      queryClient.invalidateQueries({
        queryKey: agentsQueryKeys.detail(applicationId ?? '', agentId ?? ''),
      }),
      queryClient.invalidateQueries({ queryKey: agentsQueryKeys.list(applicationId ?? '') }),
    ]);
  }

  const updateMutation = useMutation({
    mutationFn: (values: AgentFormValues) =>
      updateAgent(applicationId ?? '', agentId ?? '', toRequest(values)),
    onSuccess: async () => {
      await invalidate();
      toast.success('Agent saved');
    },
    onError: (error) => toast.error(errorMessage(error, 'Failed to save agent')),
  });

  const enableMutation = useMutation({
    mutationFn: () => enableAgent(applicationId ?? '', agentId ?? ''),
    onSuccess: async () => {
      await invalidate();
      setPendingAction(null);
      toast.success('Agent enabled');
    },
    onError: (error) => toast.error(errorMessage(error, 'Failed to enable agent')),
  });

  const runMutation = useMutation({
    mutationFn: (values: { userRequest: string; outputTitle?: string }) =>
      submitRun(applicationId ?? '', agentId ?? '', {
        userRequest: values.userRequest,
        outputTitle: values.outputTitle || null,
        idempotencyKey: crypto.randomUUID(),
      }),
    onSuccess: (response) => {
      setIsRunDialogOpen(false);
      toast.success('Run submitted');
      navigate(`/applications/${applicationId}/runs/${response.runId}`);
    },
    onError: (error) => toast.error(errorMessage(error, 'Failed to submit run')),
  });

  const disableMutation = useMutation({
    mutationFn: () => disableAgent(applicationId ?? '', agentId ?? ''),
    onSuccess: async () => {
      await invalidate();
      setPendingAction(null);
      toast.success('Agent disabled');
    },
    onError: (error) => toast.error(errorMessage(error, 'Failed to disable agent')),
  });

  const deleteMutation = useMutation({
    mutationFn: () => deleteAgent(applicationId ?? '', agentId ?? ''),
    onSuccess: async () => {
      await invalidate();
      toast.success('Agent deleted');
      navigate(`/applications/${applicationId}/agents`);
    },
    onError: (error) => toast.error(errorMessage(error, 'Failed to delete agent')),
  });

  if (agentQuery.isLoading) {
    return (
      <section aria-label="Agent detail" className="space-y-4">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-96 w-full" />
      </section>
    );
  }

  if (agentQuery.isError) {
    const notFound = agentQuery.error instanceof ApiError && agentQuery.error.status === 404;
    return (
      <section aria-label="Agent detail">
        <p role="alert" className="text-sm text-danger">
          {notFound
            ? "This agent doesn't exist or you don't have access to it."
            : errorMessage(agentQuery.error, 'Failed to load agent')}
        </p>
      </section>
    );
  }

  const agent = agentQuery.data;
  if (!agent) {
    return null;
  }

  const canEdit = capabilities.canManageAgents && agent.prompt !== null;
  const isMutating =
    updateMutation.isPending ||
    enableMutation.isPending ||
    disableMutation.isPending ||
    deleteMutation.isPending;

  return (
    <section aria-labelledby="agent-heading" className="flex h-full flex-col gap-4">
      <PageHeader
        eyebrow="Agent"
        icon={Bot}
        title={agent.name}
        meta={
          <StatusBadge status={STATUS_KIND_BY_AGENT_STATUS[agent.status]} label={agent.status} />
        }
        actions={
          <>
            {capabilities.canExecuteAgent && agent.status === 'Active' && (
              <Button size="sm" onClick={() => setIsRunDialogOpen(true)}>
                Run agent
              </Button>
            )}
            {capabilities.canManageAgents && agent.status !== 'Deleted' && (
              <>
                {agent.status === 'Active' ? (
                  <Button variant="outline" size="sm" onClick={() => setPendingAction('disable')}>
                    Disable
                  </Button>
                ) : (
                  <Button variant="outline" size="sm" onClick={() => setPendingAction('enable')}>
                    Enable
                  </Button>
                )}
                <Button variant="destructive" size="sm" onClick={() => setPendingAction('delete')}>
                  Delete
                </Button>
              </>
            )}
          </>
        }
      />

      <RunAgentDialog
        open={isRunDialogOpen}
        onOpenChange={setIsRunDialogOpen}
        agentName={agent.name}
        isPending={runMutation.isPending}
        onSubmit={(values) =>
          runMutation.mutate({ userRequest: values.userRequest, outputTitle: values.outputTitle })
        }
      />

      <AgentEditorForm
        key={agent.updatedAt ?? agent.createdAt}
        agent={agent}
        applicationId={applicationId ?? ''}
        deployments={deployments}
        canEdit={canEdit}
        isSaving={updateMutation.isPending}
        onSave={(values) => updateMutation.mutate(values)}
      />

      <ConfirmDialog
        open={pendingAction !== null}
        onOpenChange={(open) => !open && setPendingAction(null)}
        title={
          pendingAction === 'disable'
            ? 'Disable agent?'
            : pendingAction === 'enable'
              ? 'Enable agent?'
              : 'Delete agent?'
        }
        description={
          pendingAction === 'disable'
            ? `"${agent.name}" can no longer be run until re-enabled.`
            : pendingAction === 'enable'
              ? `"${agent.name}" becomes runnable again.`
              : `"${agent.name}" will be soft-deleted. Historical outputs remain viewable, but it can no longer be run.`
        }
        confirmLabel={
          pendingAction === 'disable' ? 'Disable' : pendingAction === 'enable' ? 'Enable' : 'Delete'
        }
        destructive={pendingAction === 'delete' || pendingAction === 'disable'}
        isPending={isMutating}
        onConfirm={() => {
          if (pendingAction === 'disable') {
            disableMutation.mutate();
          } else if (pendingAction === 'enable') {
            enableMutation.mutate();
          } else if (pendingAction === 'delete') {
            deleteMutation.mutate();
          }
        }}
      />
    </section>
  );
}

function mapAgentToFormValues(agent: AgentResponse): AgentFormValues {
  return {
    name: agent.name,
    description: agent.description ?? '',
    prompt: agent.prompt ?? '',
    generationDeployment: agent.generationDeployment,
    temperature: agent.temperature,
    maximumOutputTokens: agent.maximumOutputTokens,
    topP: agent.topP,
    retrievalMode: agent.retrievalMode,
    retrievalCount: agent.retrievalCount,
    minimumSimilarity: agent.minimumSimilarity,
    maximumEvidenceTokens: agent.maximumEvidenceTokens,
    includeNeighbors: agent.includeNeighbors,
    strictGrounding: agent.strictGrounding,
    citationsRequired: agent.citationsRequired,
    knowledgeScopeType: agent.knowledgeScopeType,
    selectedFileIds: agent.selectedFileIds,
  };
}

function toRequest(values: AgentFormValues): AgentFormRequest {
  return {
    name: values.name,
    description: values.description || null,
    prompt: values.prompt,
    knowledgeScopeType: values.knowledgeScopeType,
    selectedFileIds: values.knowledgeScopeType === 'Selected' ? values.selectedFileIds : null,
    generationDeployment: values.generationDeployment,
    temperature: values.temperature,
    maximumOutputTokens: values.maximumOutputTokens,
    topP: values.topP,
    retrievalMode: values.retrievalMode,
    retrievalCount: values.retrievalCount,
    minimumSimilarity: values.minimumSimilarity,
    maximumEvidenceTokens: values.maximumEvidenceTokens,
    includeNeighbors: values.includeNeighbors,
    strictGrounding: values.strictGrounding,
    citationsRequired: values.citationsRequired,
  };
}

interface AgentEditorFormProps {
  agent: AgentResponse;
  applicationId: string;
  deployments: DeploymentInfo[];
  canEdit: boolean;
  isSaving: boolean;
  onSave: (values: AgentFormValues) => void;
}

/**
 * TASK-AGT-04: the actual prompt + parameter + scope editor, split into its
 * own component so `form.reset` only ever runs against a fully-loaded
 * `AgentResponse` (the parent handles loading/error/not-found states).
 */
function AgentEditorForm({
  agent,
  applicationId,
  deployments,
  canEdit,
  isSaving,
  onSave,
}: AgentEditorFormProps) {
  const form = useForm<AgentFormValues>({
    resolver: zodResolver(agentFormSchema),
    defaultValues: mapAgentToFormValues(agent),
  });

  useEffect(() => {
    form.reset(mapAgentToFormValues(agent));
    // eslint-disable-next-line react-hooks/exhaustive-deps -- reset only when the loaded agent identity changes.
  }, [agent]);

  const promptValue = useWatch({ control: form.control, name: 'prompt' });
  const temperature = useWatch({ control: form.control, name: 'temperature' });
  const generationDeployment = useWatch({ control: form.control, name: 'generationDeployment' });
  const knowledgeScopeType = useWatch({ control: form.control, name: 'knowledgeScopeType' });
  const selectedFileIds = useWatch({ control: form.control, name: 'selectedFileIds' });

  const selectedDeployment = deployments.find(
    (deployment) => deployment.name === generationDeployment,
  );
  const charCount = promptValue.length;
  const tokenCount = estimateTokenCount(promptValue);
  const contextLimit = selectedDeployment?.maxContextTokens ?? null;
  const isOverContextLimit = contextLimit !== null && tokenCount > contextLimit;
  const activePreset = presetForTemperature(temperature);
  const topPSupported = selectedDeployment?.supportsTopP ?? false;
  const temperatureSupported = selectedDeployment?.supportsTemperature ?? true;
  const promptHidden = agent.prompt === null;

  return (
    <Form {...form}>
      <form
        onSubmit={form.handleSubmit((values) => onSave(values))}
        className="flex flex-1 flex-col gap-4"
      >
        <fieldset disabled={!canEdit || isSaving} className="flex flex-1 flex-col gap-4">
          <ResizablePanelGroup orientation="horizontal" className="h-[36rem]">
            <ResizablePanel defaultSize={65} minSize={35}>
              <Card className="flex h-full flex-col">
                <CardHeader>
                  <CardTitle>Prompt</CardTitle>
                  <CardDescription>
                    Instructions sent to the model on every run (FR-AGT-003 - never silently
                    truncated).
                  </CardDescription>
                </CardHeader>
                <CardContent className="flex min-h-0 flex-1 flex-col gap-2 px-6 pb-6">
                  {promptHidden ? (
                    <p className="text-sm text-text-muted">
                      This agent&apos;s prompt is only visible to Application Admins.
                    </p>
                  ) : (
                    <FormField
                      control={form.control}
                      name="prompt"
                      render={({ field }) => (
                        <FormItem className="flex min-h-0 flex-1 flex-col gap-2">
                          <FormControl>
                            <div className="min-h-0 flex-1 rounded-control border border-border">
                              <CodeEditor
                                value={field.value}
                                onChange={field.onChange}
                                language="markdown"
                                readOnly={!canEdit}
                                ariaLabel="Agent prompt"
                              />
                            </div>
                          </FormControl>
                          <div className="flex items-center justify-between text-xs">
                            <span className="text-text-muted">
                              {charCount.toLocaleString()} characters
                            </span>
                            <span
                              className={cn(
                                'font-medium',
                                isOverContextLimit ? 'text-danger' : 'text-text-muted',
                              )}
                            >
                              ~{tokenCount.toLocaleString()} tokens
                              {contextLimit !== null &&
                                ` / ${contextLimit.toLocaleString()} context limit`}
                            </span>
                          </div>
                          {isOverContextLimit && (
                            <p role="alert" className="text-xs text-danger">
                              This prompt likely exceeds the selected deployment&apos;s context
                              limit. It will not be truncated - saving may still succeed, but runs
                              could fail or leave little room for retrieved evidence.
                            </p>
                          )}
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                  )}
                </CardContent>
              </Card>
            </ResizablePanel>
            <ResizableHandle />
            <ResizablePanel defaultSize={35} minSize={25}>
              <div className="h-full space-y-4 overflow-y-auto pl-1 pr-1">
                <Card>
                  <CardHeader>
                    <CardTitle>Details</CardTitle>
                  </CardHeader>
                  <CardContent className="space-y-4 px-6">
                    <FormField
                      control={form.control}
                      name="name"
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Name</FormLabel>
                          <FormControl>
                            <Input {...field} />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                    <FormField
                      control={form.control}
                      name="description"
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Description</FormLabel>
                          <FormControl>
                            <Textarea {...field} placeholder="Optional" />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                  </CardContent>
                </Card>

                <Card>
                  <CardHeader>
                    <CardTitle>Model &amp; generation</CardTitle>
                  </CardHeader>
                  <CardContent className="space-y-4 px-6">
                    <FormField
                      control={form.control}
                      name="generationDeployment"
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Deployment</FormLabel>
                          <Select
                            value={field.value}
                            onValueChange={(value) => {
                              field.onChange(value);
                              const next = deployments.find((d) => d.name === value);
                              if (!next?.supportsTopP) {
                                form.setValue('topP', null);
                              }
                            }}
                          >
                            <FormControl>
                              <SelectTrigger>
                                <SelectValue placeholder="Select a deployment" />
                              </SelectTrigger>
                            </FormControl>
                            <SelectContent>
                              {deployments.map((deployment) => (
                                <SelectItem key={deployment.name} value={deployment.name}>
                                  {deployment.name}
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                          <FormMessage />
                        </FormItem>
                      )}
                    />

                    <div className="space-y-2">
                      <p className="text-sm font-medium text-text-primary">Preset</p>
                      <div className="flex flex-wrap gap-2">
                        {GENERATION_PRESETS.map((preset) => (
                          <Button
                            key={preset}
                            type="button"
                            size="sm"
                            variant={activePreset === preset ? 'default' : 'outline'}
                            disabled={preset === 'Custom' || !temperatureSupported}
                            onClick={() => {
                              if (preset !== 'Custom') {
                                form.setValue(
                                  'temperature',
                                  GENERATION_PRESET_TEMPERATURES[preset],
                                  {
                                    shouldDirty: true,
                                  },
                                );
                              }
                            }}
                          >
                            {preset}
                          </Button>
                        ))}
                      </div>
                    </div>

                    {temperatureSupported ? (
                      <FormField
                        control={form.control}
                        name="temperature"
                        render={({ field }) => (
                          <FormItem>
                            <FormLabel>Temperature</FormLabel>
                            <FormControl>
                              <Input
                                type="number"
                                step={0.1}
                                min={0}
                                max={2}
                                value={field.value}
                                onChange={(event) => field.onChange(event.target.valueAsNumber)}
                              />
                            </FormControl>
                            <FormDescription>0 = deterministic, 2 = most creative.</FormDescription>
                            <FormMessage />
                          </FormItem>
                        )}
                      />
                    ) : (
                      <div className="space-y-2">
                        <p className="text-sm font-medium text-text-primary">Temperature</p>
                        <p className="text-sm text-text-muted">
                          {selectedDeployment?.name ?? 'This deployment'} is a reasoning model that
                          only accepts its default temperature - the setting is ignored and hidden
                          to avoid confusion.
                        </p>
                      </div>
                    )}

                    <FormField
                      control={form.control}
                      name="maximumOutputTokens"
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Maximum output tokens</FormLabel>
                          <FormControl>
                            <Input
                              type="number"
                              min={1}
                              value={field.value}
                              onChange={(event) => field.onChange(event.target.valueAsNumber)}
                            />
                          </FormControl>
                          {selectedDeployment?.maxOutputTokens && (
                            <FormDescription>
                              Deployment limit:{' '}
                              {selectedDeployment.maxOutputTokens.toLocaleString()}
                            </FormDescription>
                          )}
                          <FormMessage />
                        </FormItem>
                      )}
                    />

                    {topPSupported && (
                      <FormField
                        control={form.control}
                        name="topP"
                        render={({ field }) => (
                          <FormItem>
                            <FormLabel>Top P</FormLabel>
                            <FormControl>
                              <Input
                                type="number"
                                step={0.05}
                                min={0}
                                max={1}
                                value={field.value ?? ''}
                                onChange={(event) =>
                                  field.onChange(
                                    event.target.value === '' ? null : event.target.valueAsNumber,
                                  )
                                }
                              />
                            </FormControl>
                            <FormMessage />
                          </FormItem>
                        )}
                      />
                    )}
                  </CardContent>
                </Card>

                <Card>
                  <CardHeader>
                    <CardTitle>Retrieval</CardTitle>
                  </CardHeader>
                  <CardContent className="space-y-4 px-6">
                    <FormField
                      control={form.control}
                      name="retrievalMode"
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Mode</FormLabel>
                          <Select value={field.value} onValueChange={field.onChange}>
                            <FormControl>
                              <SelectTrigger>
                                <SelectValue />
                              </SelectTrigger>
                            </FormControl>
                            <SelectContent>
                              <SelectItem value="Semantic">Semantic</SelectItem>
                              <SelectItem value="Hybrid">Hybrid</SelectItem>
                            </SelectContent>
                          </Select>
                        </FormItem>
                      )}
                    />
                    <FormField
                      control={form.control}
                      name="retrievalCount"
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Chunks retrieved</FormLabel>
                          <FormControl>
                            <Input
                              type="number"
                              min={1}
                              max={50}
                              value={field.value}
                              onChange={(event) => field.onChange(event.target.valueAsNumber)}
                            />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                    <FormField
                      control={form.control}
                      name="minimumSimilarity"
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Minimum similarity</FormLabel>
                          <FormControl>
                            <Input
                              type="number"
                              step={0.05}
                              min={0}
                              max={1}
                              value={field.value}
                              onChange={(event) => field.onChange(event.target.valueAsNumber)}
                            />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                    <FormField
                      control={form.control}
                      name="maximumEvidenceTokens"
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Maximum evidence tokens</FormLabel>
                          <FormControl>
                            <Input
                              type="number"
                              min={1}
                              value={field.value}
                              onChange={(event) => field.onChange(event.target.valueAsNumber)}
                            />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                    <FormField
                      control={form.control}
                      name="includeNeighbors"
                      render={({ field }) => (
                        <FormItem>
                          <label className="flex items-center gap-2 text-sm font-medium text-text-primary">
                            <input
                              type="checkbox"
                              checked={field.value}
                              onChange={(event) => field.onChange(event.target.checked)}
                              className="size-4 rounded-control border border-border accent-primary"
                            />
                            Include neighboring chunks
                          </label>
                        </FormItem>
                      )}
                    />
                    <FormField
                      control={form.control}
                      name="strictGrounding"
                      render={({ field }) => (
                        <FormItem>
                          <label className="flex items-center gap-2 text-sm font-medium text-text-primary">
                            <input
                              type="checkbox"
                              checked={field.value}
                              onChange={(event) => field.onChange(event.target.checked)}
                              className="size-4 rounded-control border border-border accent-primary"
                            />
                            Strict grounding
                          </label>
                        </FormItem>
                      )}
                    />
                    <FormField
                      control={form.control}
                      name="citationsRequired"
                      render={({ field }) => (
                        <FormItem>
                          <label className="flex items-center gap-2 text-sm font-medium text-text-primary">
                            <input
                              type="checkbox"
                              checked={field.value}
                              onChange={(event) => field.onChange(event.target.checked)}
                              className="size-4 rounded-control border border-border accent-primary"
                            />
                            Citations required
                          </label>
                        </FormItem>
                      )}
                    />
                  </CardContent>
                </Card>

                <Card>
                  <CardHeader>
                    <CardTitle>Knowledge scope</CardTitle>
                    <CardDescription>Which files this agent may retrieve from.</CardDescription>
                  </CardHeader>
                  <CardContent className="px-6">
                    <KnowledgeScopeField
                      applicationId={applicationId}
                      scopeType={knowledgeScopeType}
                      selectedFileIds={selectedFileIds}
                      onScopeTypeChange={(scopeType) =>
                        form.setValue('knowledgeScopeType', scopeType, { shouldDirty: true })
                      }
                      onSelectedFileIdsChange={(fileIds) =>
                        form.setValue('selectedFileIds', fileIds, { shouldDirty: true })
                      }
                      disabled={!canEdit}
                    />
                  </CardContent>
                </Card>
              </div>
            </ResizablePanel>
          </ResizablePanelGroup>

          {canEdit && (
            <div className="flex items-center justify-end gap-2">
              <Button type="submit" disabled={isSaving}>
                {isSaving ? 'Saving...' : 'Save'}
              </Button>
            </div>
          )}
        </fieldset>
      </form>
    </Form>
  );
}
