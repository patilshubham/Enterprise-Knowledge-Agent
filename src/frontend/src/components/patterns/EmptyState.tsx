import type { ReactNode } from 'react';
import type { LucideIcon } from 'lucide-react';
import { cn } from '@/lib/utils';
import { chipStyle } from './tone';
import type { Tone } from './tone';

export interface EmptyStateProps {
  icon: LucideIcon;
  title: string;
  description?: ReactNode;
  /** Primary/secondary actions (buttons). */
  action?: ReactNode;
  tone?: Tone;
  className?: string;
  /** Compact variant for inline/section empties. */
  compact?: boolean;
}

/**
 * Redesign: a friendly, guided empty state. Every "nothing here yet" surface
 * uses this so the app teaches the next step instead of showing a blank panel
 * - a large icon in a tinted halo, a clear title, a one-line explanation, and
 * a primary call-to-action.
 */
export function EmptyState({
  icon: Icon,
  title,
  description,
  action,
  tone = 'accent',
  className,
  compact,
}: EmptyStateProps) {
  return (
    <div
      className={cn(
        'flex flex-col items-center justify-center gap-3 text-center',
        compact ? 'px-6 py-8' : 'px-6 py-14',
        className,
      )}
    >
      <span
        className={cn('icon-chip', compact ? 'size-11' : 'size-14')}
        style={chipStyle(tone)}
        aria-hidden="true"
      >
        <Icon className={compact ? 'size-5' : 'size-7'} />
      </span>
      <div className="space-y-1">
        <p className="text-base font-semibold text-text-primary">{title}</p>
        {description && (
          <p className="mx-auto max-w-sm text-sm text-text-muted">{description}</p>
        )}
      </div>
      {action && <div className="mt-1 flex flex-wrap items-center justify-center gap-2">{action}</div>}
    </div>
  );
}
