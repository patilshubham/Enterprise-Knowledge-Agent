import { z } from 'zod';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

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
import { Button } from '@/components/ui/button';

const schema = z.object({
  title: z.string().min(3, 'Title must be at least 3 characters.'),
});

function TestForm({ onSubmit }: { onSubmit: (values: z.infer<typeof schema>) => void }) {
  const form = useForm<z.infer<typeof schema>>({
    resolver: zodResolver(schema),
    defaultValues: { title: '' },
  });

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(onSubmit)} noValidate>
        <FormField
          control={form.control}
          name="title"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Title</FormLabel>
              <FormControl>
                <Input {...field} />
              </FormControl>
              <FormDescription>Shown to reviewers.</FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />
        <Button type="submit">Submit</Button>
      </form>
    </Form>
  );
}

describe('Form primitives (TASK-UX-07)', () => {
  it('shows the zod validation error when submitted invalid', async () => {
    const onSubmit = vi.fn();
    render(<TestForm onSubmit={onSubmit} />);

    fireEvent.click(screen.getByRole('button', { name: 'Submit' }));

    expect(await screen.findByText('Title must be at least 3 characters.')).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('associates the label, description and control via aria attributes', () => {
    render(<TestForm onSubmit={vi.fn()} />);

    const input = screen.getByLabelText('Title');
    expect(input).toHaveAccessibleDescription('Shown to reviewers.');
  });

  it('calls onSubmit with parsed values once validation passes', async () => {
    const onSubmit = vi.fn();
    render(<TestForm onSubmit={onSubmit} />);

    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'A valid title' } });
    fireEvent.click(screen.getByRole('button', { name: 'Submit' }));

    await screen.findByRole('button', { name: 'Submit' });
    expect(onSubmit).toHaveBeenCalledWith({ title: 'A valid title' }, expect.anything());
    expect(screen.queryByText('Title must be at least 3 characters.')).not.toBeInTheDocument();
  });
});
