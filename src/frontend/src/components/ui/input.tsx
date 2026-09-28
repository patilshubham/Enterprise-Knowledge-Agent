import type { ComponentProps } from 'react';
import { cn } from '@/lib/utils';

function Input({ className, type, ...props }: ComponentProps<'input'>) {
  return (
    <input
      type={type}
      data-slot="input"
      className={cn(
        'flex h-9 w-full min-w-0 rounded-control border border-border bg-surface px-3 py-1 text-sm text-text-primary shadow-xs transition-colors outline-none',
        'placeholder:text-text-muted',
        'focus-visible:border-ring focus-visible:ring-2 focus-visible:ring-ring/40',
        'aria-invalid:border-danger aria-invalid:ring-2 aria-invalid:ring-danger/40',
        'disabled:cursor-not-allowed disabled:opacity-50',
        'file:inline-flex file:h-7 file:border-0 file:bg-transparent file:text-sm file:font-medium file:text-text-primary',
        className,
      )}
      {...props}
    />
  );
}

export { Input };
