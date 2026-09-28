import { useQuery } from '@tanstack/react-query';
import { ChevronDown, Settings2 } from 'lucide-react';

import type { ChatAdvancedConfigRequest } from '@/api/chat';
import { chatDeployments, deploymentsQueryKeys, listDeployments } from '@/api/deployments';
import {
  knowledgeFilesQueryKeys,
  listKnowledgeFiles,
  type KnowledgeFileResponse,
} from '@/api/knowledgeFiles';
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { cn } from '@/lib/utils';

const KNOWLEDGE_FILE_STATUS_READY = 2;

export interface ChatAdvancedConfigProps {
  applicationId: string;
  value: ChatAdvancedConfigRequest;
  onChange: (value: ChatAdvancedConfigRequest) => void;
  disabled?: boolean;
  /**
   * Read-only view of an existing conversation's immutable settings. The panel
   * stays expandable so the user can review what the chat is using, but every
   * control is disabled (settings are fixed once a conversation has started).
   */
  readOnly?: boolean;
  /** In read-only mode, whether the conversation was started with a custom persona. */
  hasCustomSystemPrompt?: boolean;
}

/**
 * Optional, collapsed-by-default configuration applied when a conversation is
 * first created. Leaving it untouched uses the server's sensible defaults
 * (config-free experience); expanding it lets power users scope retrieval to
 * specific files, pick a deployment, tune retrieval mode/temperature, or set a
 * custom persona.
 */
export function ChatAdvancedConfig({
  applicationId,
  value,
  onChange,
  disabled,
  readOnly = false,
  hasCustomSystemPrompt = false,
}: ChatAdvancedConfigProps) {
  const deploymentsQuery = useQuery({
    queryKey: deploymentsQueryKeys.list(),
    queryFn: listDeployments,
  });

  const filesQuery = useQuery({
    queryKey: knowledgeFilesQueryKeys.list(applicationId),
    queryFn: () => listKnowledgeFiles(applicationId),
  });

  const deployments = deploymentsQuery.data ? chatDeployments(deploymentsQuery.data) : [];
  const readyFiles: KnowledgeFileResponse[] = (filesQuery.data ?? []).filter(
    (file) => file.status === KNOWLEDGE_FILE_STATUS_READY && file.isEnabled,
  );

  // The effective deployment is the one explicitly picked, or (when left on
  // "Default") the server's configured chat deployment - surfaced as the first
  // chat-purpose entry. Reasoning models (e.g. gpt-5.5) reject a custom
  // temperature, so the slider is hidden for them (the value is ignored
  // server-side anyway).
  const effectiveDeployment = value.generationDeployment
    ? deployments.find((deployment) => deployment.name === value.generationDeployment)
    : deployments[0];
  const temperatureSupported = effectiveDeployment?.supportsTemperature ?? true;

  // When viewing an existing conversation's immutable settings, every control is
  // disabled but the panel stays expandable so the user can still review them.
  const controlsDisabled = disabled || readOnly;

  const scope = value.knowledgeScope ?? 'AllReadyEnabled';
  const selectedFileIds = value.selectedFileIds ?? [];

  const toggleFile = (fileId: string) => {
    const next = selectedFileIds.includes(fileId)
      ? selectedFileIds.filter((id) => id !== fileId)
      : [...selectedFileIds, fileId];
    onChange({ ...value, selectedFileIds: next });
  };

  return (
    <Collapsible className="rounded-xl border border-border bg-surface">
      <CollapsibleTrigger
        disabled={disabled}
        className="group flex w-full items-center justify-between gap-2 px-4 py-3 text-sm font-medium text-text-secondary disabled:cursor-not-allowed"
      >
        <span className="flex items-center gap-2">
          <Settings2 className="h-4 w-4" />
          {readOnly ? 'Conversation settings' : 'Advanced configuration'}
          <span className="font-normal text-text-muted">
            {readOnly ? '(read-only)' : '(optional)'}
          </span>
        </span>
        <ChevronDown className="h-4 w-4 transition-transform group-data-[state=open]:rotate-180" />
      </CollapsibleTrigger>

      <CollapsibleContent className="space-y-4 border-t border-border px-4 py-4">
        {readOnly && (
          <p className="text-xs text-text-muted">
            These settings were chosen when this chat started and are fixed for the
            conversation. Start a new chat to use different settings.
          </p>
        )}
        <div className="space-y-2">
          <Label htmlFor="chat-scope">Knowledge scope</Label>
          <Select
            value={scope}
            onValueChange={(next) =>
              onChange({
                ...value,
                knowledgeScope: next as ChatAdvancedConfigRequest['knowledgeScope'],
              })
            }
            disabled={controlsDisabled}
          >
            <SelectTrigger id="chat-scope">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="AllReadyEnabled">All ready files</SelectItem>
              <SelectItem value="Selected">Selected files only</SelectItem>
            </SelectContent>
          </Select>
        </div>

        {scope === 'Selected' && (
          <div className="space-y-2">
            <Label>Files to search</Label>
            {readyFiles.length === 0 ? (
              <p className="text-xs text-text-muted">
                No ready knowledge files are available for this application.
              </p>
            ) : (
              <ul className="max-h-40 space-y-1 overflow-y-auto rounded-lg border border-border p-2">
                {readyFiles.map((file) => (
                  <li key={file.id}>
                    <label
                      className={cn(
                        'flex cursor-pointer items-center gap-2 rounded px-2 py-1 text-sm hover:bg-surface-raised',
                        controlsDisabled && 'cursor-not-allowed opacity-60',
                      )}
                    >
                      <input
                        type="checkbox"
                        className="h-4 w-4 accent-accent"
                        checked={selectedFileIds.includes(file.id)}
                        onChange={() => toggleFile(file.id)}
                        disabled={controlsDisabled}
                      />
                      <span className="truncate text-text-primary">{file.displayName}</span>
                    </label>
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}

        <div className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-2">
            <Label htmlFor="chat-deployment">Model deployment</Label>
            <Select
              value={value.generationDeployment ?? ''}
              onValueChange={(next) => onChange({ ...value, generationDeployment: next })}
              disabled={controlsDisabled || deployments.length === 0}
            >
              <SelectTrigger id="chat-deployment">
                <SelectValue placeholder="Default" />
              </SelectTrigger>
              <SelectContent>
                {deployments.map((deployment) => (
                  <SelectItem key={deployment.name} value={deployment.name}>
                    {deployment.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label htmlFor="chat-retrieval">Retrieval mode</Label>
            <Select
              value={value.retrievalMode ?? 'Semantic'}
              onValueChange={(next) =>
                onChange({
                  ...value,
                  retrievalMode: next as ChatAdvancedConfigRequest['retrievalMode'],
                })
              }
              disabled={controlsDisabled}
            >
              <SelectTrigger id="chat-retrieval">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="Semantic">Semantic</SelectItem>
                <SelectItem value="Hybrid">Hybrid (semantic + keyword)</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </div>

        <div className="space-y-2">
          <Label htmlFor="chat-temperature">
            {temperatureSupported
              ? `Temperature: ${(value.temperature ?? 0.2).toFixed(2)}`
              : 'Temperature'}
          </Label>
          {temperatureSupported ? (
            <input
              id="chat-temperature"
              type="range"
              min={0}
              max={1}
              step={0.05}
              value={value.temperature ?? 0.2}
              onChange={(event) => onChange({ ...value, temperature: Number(event.target.value) })}
              disabled={controlsDisabled}
              className="w-full accent-accent"
            />
          ) : (
            <p className="text-xs text-text-muted">
              {effectiveDeployment?.name ?? 'This deployment'} is a reasoning model that only
              accepts its default temperature - the control is hidden and any value is ignored.
            </p>
          )}
        </div>

        <div className="space-y-2">
          <Label htmlFor="chat-persona">System prompt (persona)</Label>
          {readOnly ? (
            <p className="text-sm text-text-muted">
              {hasCustomSystemPrompt
                ? 'A custom persona was set for this conversation.'
                : 'Using the default grounded-assistant persona.'}
            </p>
          ) : (
            <Textarea
              id="chat-persona"
              rows={3}
              placeholder="Leave blank to use the default grounded-assistant persona."
              value={value.systemPrompt ?? ''}
              onChange={(event) => onChange({ ...value, systemPrompt: event.target.value })}
              disabled={controlsDisabled}
            />
          )}
        </div>
      </CollapsibleContent>
    </Collapsible>
  );
}
