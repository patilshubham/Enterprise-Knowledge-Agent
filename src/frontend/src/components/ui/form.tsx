import type { ComponentProps } from 'react';
import { Slot } from '@radix-ui/react-slot';
import { Controller, FormProvider } from 'react-hook-form';
import type { ControllerProps, FieldPath, FieldValues } from 'react-hook-form';

import { cn } from '@/lib/utils';
import { Label } from '@/components/ui/label';
import {
  FormFieldContext,
  FormItemContext,
  useFormField,
  useFormItemId,
} from './form-field-context';

/**
 * TASK-UX-07 (Module 37): shared form pattern - `zod` schema -> `react-hook-form`
 * -> these shadcn-style fields - giving every form in the app the same
 * validation/error/disabled states. Pair with `@hookform/resolvers/zod`:
 *
 * ```tsx
 * const form = useForm({ resolver: zodResolver(schema) });
 * <Form {...form}>
 *   <FormField control={form.control} name="title" render={({ field }) => (
 *     <FormItem>
 *       <FormLabel>Title</FormLabel>
 *       <FormControl><Input {...field} /></FormControl>
 *       <FormDescription>Shown to reviewers.</FormDescription>
 *       <FormMessage />
 *     </FormItem>
 *   )} />
 * </Form>
 * ```
 */
const Form = FormProvider;

function FormField<
  TFieldValues extends FieldValues = FieldValues,
  TName extends FieldPath<TFieldValues> = FieldPath<TFieldValues>,
>(props: ControllerProps<TFieldValues, TName>) {
  return (
    <FormFieldContext.Provider value={{ name: props.name }}>
      <Controller {...props} />
    </FormFieldContext.Provider>
  );
}

function FormItem({ className, ...props }: ComponentProps<'div'>) {
  const id = useFormItemId();
  return (
    <FormItemContext.Provider value={{ id }}>
      <div data-slot="form-item" className={cn('grid gap-2', className)} {...props} />
    </FormItemContext.Provider>
  );
}

function FormLabel({ className, ...props }: ComponentProps<typeof Label>) {
  const { error, formItemId } = useFormField();
  return (
    <Label
      data-slot="form-label"
      data-error={!!error}
      htmlFor={formItemId}
      className={cn('data-[error=true]:text-danger', className)}
      {...props}
    />
  );
}

function FormControl({ ...props }: ComponentProps<typeof Slot>) {
  const { error, formItemId, formDescriptionId, formMessageId } = useFormField();
  return (
    <Slot
      data-slot="form-control"
      id={formItemId}
      aria-describedby={!error ? formDescriptionId : `${formDescriptionId} ${formMessageId}`}
      aria-invalid={!!error}
      {...props}
    />
  );
}

function FormDescription({ className, ...props }: ComponentProps<'p'>) {
  const { formDescriptionId } = useFormField();
  return (
    <p
      data-slot="form-description"
      id={formDescriptionId}
      className={cn('text-sm text-text-muted', className)}
      {...props}
    />
  );
}

function FormMessage({ className, children, ...props }: ComponentProps<'p'>) {
  const { error, formMessageId } = useFormField();
  const body = error ? String(error.message ?? '') : children;
  if (!body) {
    return null;
  }

  return (
    <p
      data-slot="form-message"
      id={formMessageId}
      className={cn('text-sm font-medium text-danger', className)}
      {...props}
    >
      {body}
    </p>
  );
}

export { Form, FormField, FormItem, FormLabel, FormControl, FormDescription, FormMessage };
