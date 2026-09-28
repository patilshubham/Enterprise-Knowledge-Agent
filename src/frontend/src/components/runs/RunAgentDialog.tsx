import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';

const runAgentFormSchema = z.object({
  agentId: z.string().optional(),
  userRequest: z.string().trim().min(1, 'Request is required').max(20_000, 'Request is too long'),
  outputTitle: z.string().trim().max(200, 'Title is too long').optional(),
});

export type RunAgentFormValues = z.infer<typeof runAgentFormSchema>;

export interface RunAgentDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSubmit: (values: RunAgentFormValues) => void;
  isPending?: boolean;
  /** Fixed agent name when already in an agent's own context - hides the agent picker. */
  agentName?: string;
  /** Selectable agents when not in a fixed-agent context (e.g. the runs dashboard). */
  agentOptions?: { id: string; name: string }[];
}

const DEFAULT_VALUES: RunAgentFormValues = { agentId: '', userRequest: '', outputTitle: '' };

/**
 * The "Run agent" trigger dialog. The backend has always supported
 * `POST .../agents/{agentId}/runs` (TASK-RUN-01), but no UI ever called it -
 * the command palette's "Run agent" entry just navigated to the agents list.
 * Collects the free-text request and an optional output title; the caller
 * submits and navigates to the resulting run's detail page.
 */
export function RunAgentDialog({
  open,
  onOpenChange,
  onSubmit,
  isPending = false,
  agentName,
  agentOptions,
}: RunAgentDialogProps) {
  const form = useForm<RunAgentFormValues>({
    resolver: zodResolver(runAgentFormSchema),
    defaultValues: DEFAULT_VALUES,
  });

  useEffect(() => {
    if (open) {
      form.reset(DEFAULT_VALUES);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- reset only when the dialog opens.
  }, [open]);

  function handleSubmit(values: RunAgentFormValues) {
    if (agentOptions && !values.agentId) {
      form.setError('agentId', { message: 'Select an agent' });
      return;
    }
    onSubmit(values);
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Run agent</DialogTitle>
          <DialogDescription>
            {agentName
              ? `Submits a new run of "${agentName}" against its current live configuration.`
              : "Submits a new run against the selected agent's current live configuration."}
          </DialogDescription>
        </DialogHeader>
        <Form {...form}>
          <form
            id="run-agent-form"
            onSubmit={form.handleSubmit(handleSubmit)}
            className="grid gap-4"
          >
            {agentOptions && (
              <FormField
                control={form.control}
                name="agentId"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Agent</FormLabel>
                    <Select value={field.value ?? ''} onValueChange={field.onChange}>
                      <FormControl>
                        <SelectTrigger>
                          <SelectValue placeholder="Select an agent" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        {agentOptions.map((agent) => (
                          <SelectItem key={agent.id} value={agent.id}>
                            {agent.name}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />
            )}
            <FormField
              control={form.control}
              name="userRequest"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Request</FormLabel>
                  <FormControl>
                    <Textarea
                      {...field}
                      autoFocus
                      rows={5}
                      placeholder="What should the agent do?"
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="outputTitle"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Output title</FormLabel>
                  <FormControl>
                    <Input {...field} placeholder="Optional" />
                  </FormControl>
                  <FormDescription>
                    Shown in the outputs library once the run completes.
                  </FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />
          </form>
        </Form>
        <DialogFooter>
          <Button
            variant="outline"
            onClick={() => onOpenChange(false)}
            disabled={isPending}
          >
            Cancel
          </Button>
          <Button type="submit" form="run-agent-form" disabled={isPending}>
            {isPending ? 'Submitting...' : 'Run'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
