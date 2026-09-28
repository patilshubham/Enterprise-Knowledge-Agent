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

const applicationFormSchema = z.object({
  name: z.string().trim().min(1, 'Name is required').max(200, 'Name is too long'),
  description: z.string().trim().max(2000, 'Description is too long').optional(),
  allowViewerExecution: z.boolean(),
});

export type ApplicationFormValues = z.infer<typeof applicationFormSchema>;

export interface ApplicationFormDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  mode: 'create' | 'edit';
  initialValues?: ApplicationFormValues;
  onSubmit: (values: ApplicationFormValues) => void;
  isPending?: boolean;
}

const DEFAULT_VALUES: ApplicationFormValues = {
  name: '',
  description: '',
  allowViewerExecution: false,
};

/**
 * TASK-APP-03: shared create/edit form for applications (FR-APP-001/FR-APP-006).
 * `allowViewerExecution` is included directly in the create/edit payload per
 * TASK-APP-06's contract - the server-side `CanExecuteAgent` policy is what
 * actually enforces it once module 19 (run submission) exists.
 */
export function ApplicationFormDialog({
  open,
  onOpenChange,
  mode,
  initialValues,
  onSubmit,
  isPending = false,
}: ApplicationFormDialogProps) {
  const form = useForm<ApplicationFormValues>({
    resolver: zodResolver(applicationFormSchema),
    defaultValues: initialValues ?? DEFAULT_VALUES,
  });

  useEffect(() => {
    if (open) {
      form.reset(initialValues ?? DEFAULT_VALUES);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- reset only when the dialog opens or its target application changes.
  }, [open, initialValues]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{mode === 'create' ? 'Create application' : 'Edit application'}</DialogTitle>
          <DialogDescription>
            {mode === 'create'
              ? 'Applications group knowledge, agents, and outputs under a single workspace.'
              : 'Update the name, description, or viewer-execution setting.'}
          </DialogDescription>
        </DialogHeader>
        <Form {...form}>
          <form id="application-form" onSubmit={form.handleSubmit(onSubmit)} className="grid gap-4">
            <FormField
              control={form.control}
              name="name"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Name</FormLabel>
                  <FormControl>
                    <Input {...field} autoFocus placeholder="e.g. Contract Review" />
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
            <FormField
              control={form.control}
              name="allowViewerExecution"
              render={({ field }) => (
                <FormItem>
                  <label className="flex items-center gap-2 text-sm font-medium text-text-primary">
                    <input
                      type="checkbox"
                      checked={field.value}
                      onChange={(event) => field.onChange(event.target.checked)}
                      className="size-4 rounded-control border border-border accent-primary"
                    />
                    Allow viewers to run agents
                  </label>
                  <FormDescription>
                    When enabled, ApplicationViewers may submit agent runs, not just view results.
                  </FormDescription>
                </FormItem>
              )}
            />
          </form>
        </Form>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={isPending}>
            Cancel
          </Button>
          <Button type="submit" form="application-form" disabled={isPending}>
            {mode === 'create' ? 'Create' : 'Save changes'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
