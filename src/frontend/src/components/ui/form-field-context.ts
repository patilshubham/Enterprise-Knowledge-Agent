import { createContext, useContext, useId } from 'react';
import { useFormContext, useFormState } from 'react-hook-form';
import type { FieldPath, FieldValues } from 'react-hook-form';

/**
 * TASK-UX-07 (Module 37): context/hook split into its own module so
 * `form.tsx` only exports components (react-refresh's
 * only-export-components rule requires this for Fast Refresh to work).
 */
export interface FormFieldContextValue<
  TFieldValues extends FieldValues = FieldValues,
  TName extends FieldPath<TFieldValues> = FieldPath<TFieldValues>,
> {
  name: TName;
}

export const FormFieldContext = createContext<FormFieldContextValue | null>(null);

export interface FormItemContextValue {
  id: string;
}

export const FormItemContext = createContext<FormItemContextValue | null>(null);

/** Generates and provides the stable id consumed by `FormItem`'s children. */
export function useFormItemId(): string {
  return useId();
}

/**
 * Wires a form field's name/id/error state to `aria-describedby`/`aria-invalid`
 * attributes for `FormLabel`, `FormControl`, `FormDescription`, and `FormMessage`.
 * Must be called from a component rendered inside both a `<FormField>` and a
 * `<FormItem>`.
 */
export function useFormField() {
  const fieldContext = useContext(FormFieldContext);
  const itemContext = useContext(FormItemContext);
  const { getFieldState } = useFormContext();
  const formState = useFormState({ name: fieldContext?.name });

  if (!fieldContext) {
    throw new Error('useFormField must be used within a <FormField>');
  }
  if (!itemContext) {
    throw new Error('useFormField must be used within a <FormItem>');
  }

  const fieldState = getFieldState(fieldContext.name, formState);
  const { id } = itemContext;

  return {
    id,
    name: fieldContext.name,
    formItemId: `${id}-form-item`,
    formDescriptionId: `${id}-form-item-description`,
    formMessageId: `${id}-form-item-message`,
    ...fieldState,
  };
}
