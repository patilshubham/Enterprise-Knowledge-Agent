import type { ReactNode } from 'react';
import type { LucideIcon } from 'lucide-react';
import { cn } from '@/lib/utils';
import { chipStyle } from './tone';
import type { Tone } from './tone';

export interface PageHeaderProps {
  /** Small uppercase label above the title (e.g. section / context). */
  eyebrow?: string;
  title: ReactNode;
  description?: ReactNode;
  /** Optional leading icon rendered in a tinted chip. */
  icon?: LucideIcon;
  iconTone?: Tone;
  /** Right-aligned actions (buttons, menus). */
  actions?: ReactNode;
  /** Optional status/meta row rendered under the description. */
  meta?: ReactNode;
  className?: string;
}

/**
 * Redesign: one consistent, spacious page header used across every page so the
 * app reads as a coherent product instead of a set of bare `<h1>`s. Provides
 * an optional icon chip, eyebrow, title, description, a meta row, and a
 * right-aligned actions slot that wraps gracefully on narrow screens.
 */
export function PageHeader({
  eyebrow,
  title,
  description,
  icon: Icon,
  iconTone = 'accent',
  actions,
  meta,
  className,
}: PageHeaderProps) {
  return (
    <div className={cn('flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between', className)}>
      <div className="flex min-w-0 items-start gap-4">
        {Icon && (
          <span
            className="icon-chip mt-0.5 hidden size-11 shrink-0 sm:inline-flex"
            style={chipStyle(iconTone)}
            aria-hidden="true"
          >
            <Icon className="size-5" />
          </span>
        )}
        <div className="min-w-0 space-y-1">
          {eyebrow && (
            <p className="text-xs font-semibold uppercase tracking-[0.12em] text-text-muted">
              {eyebrow}
            </p>
          )}
          <h1 className="truncate text-2xl font-semibold tracking-tight text-text-primary">
            {title}
          </h1>
          {description && (
            <p className="max-w-2xl text-sm text-text-secondary">{description}</p>
          )}
          {meta && <div className="flex flex-wrap items-center gap-3 pt-1">{meta}</div>}
        </div>
      </div>
      {actions && <div className="flex shrink-0 flex-wrap items-center gap-2">{actions}</div>}
    </div>
  );
}
