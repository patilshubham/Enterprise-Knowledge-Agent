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
import { Textarea } from '@/components/ui/textarea';

const agentCreateFormSchema = z.object({
  name: z.string().trim().min(1, 'Name is required').max(200, 'Name is too long'),
  description: z.string().trim().max(2000, 'Description is too long').optional(),
});

export type AgentCreateFormValues = z.infer<typeof agentCreateFormSchema>;

export interface AgentCreateDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSubmit: (values: AgentCreateFormValues) => void;
  isPending?: boolean;
  /** Shown when no chat deployment is configured - creation is blocked without one. */
  blockedReason?: string | null;
}

const DEFAULT_VALUES: AgentCreateFormValues = { name: '', description: '' };

/**
 * TASK-AGT-04: lightweight "start an agent" dialog (name + description only).
 * The rest of the configuration (prompt, deployment, generation/retrieval
 * params, knowledge scope) is filled with sane defaults on creation and then
 * refined in the full agent editor (`AgentDetailPage`) the user is routed to
 * immediately afterward - mirrors the upload-then-detail flow already used
 * by knowledge files.
 */
export function AgentCreateDialog({
  open,
  onOpenChange,
  onSubmit,
  isPending = false,
  blockedReason,
}: AgentCreateDialogProps) {
  const form = useForm<AgentCreateFormValues>({
    resolver: zodResolver(agentCreateFormSchema),
    defaultValues: DEFAULT_VALUES,
  });

  useEffect(() => {
    if (open) {
      form.reset(DEFAULT_VALUES);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- reset only when the dialog opens.
  }, [open]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Create agent</DialogTitle>
          <DialogDescription>
            Give it a name to get started - you&apos;ll configure the prompt, model, and knowledge
            scope next.
          </DialogDescription>
        </DialogHeader>
        {blockedReason ? (
          <p role="alert" className="text-sm text-danger">
            {blockedReason}
          </p>
        ) : (
          <Form {...form}>
            <form
              id="agent-create-form"
              onSubmit={form.handleSubmit(onSubmit)}
              className="grid gap-4"
            >
              <FormField
                control={form.control}
                name="name"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Name</FormLabel>
                    <FormControl>
                      <Input {...field} autoFocus placeholder="e.g. Contract Summarizer" />
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
                    <FormDescription>
                      What this agent is for - shown in the agent list.
                    </FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </form>
          </Form>
        )}
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={isPending}>
            Cancel
          </Button>
          {!blockedReason && (
            <Button type="submit" form="agent-create-form" disabled={isPending}>
              Create
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
