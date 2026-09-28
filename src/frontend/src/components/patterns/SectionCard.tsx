import type { ReactNode } from 'react';
import type { LucideIcon } from 'lucide-react';
import { cn } from '@/lib/utils';
import { chipStyle } from './tone';
import type { Tone } from './tone';

export interface SectionCardProps {
  title: ReactNode;
  description?: ReactNode;
  icon?: LucideIcon;
  iconTone?: Tone;
  /** Right-aligned header actions (link, button, menu). */
  action?: ReactNode;
  children?: ReactNode;
  /** Removes the inner content padding (e.g. when embedding a table/list). */
  flush?: boolean;
  className?: string;
  contentClassName?: string;
}

/**
 * Redesign: the standard content container - an elevated "glass" panel with a
 * gradient hairline, an icon-chipped header, and a right-aligned action slot.
 * Replaces the flat, equal-weight shadcn `<Card>` stacks that made pages feel
 * CRUD-like.
 */
export function SectionCard({
  title,
  description,
  icon: Icon,
  iconTone = 'accent',
  action,
  children,
  flush,
  className,
  contentClassName,
}: SectionCardProps) {
  return (
    <section
      className={cn(
        'surface-panel rounded-card shadow-card',
        className,
      )}
    >
      <header className="flex items-start justify-between gap-4 px-5 pt-5">
        <div className="flex min-w-0 items-start gap-3">
          {Icon && (
            <span
              className="icon-chip size-9 shrink-0"
              style={chipStyle(iconTone)}
              aria-hidden="true"
            >
              <Icon className="size-[18px]" />
            </span>
          )}
          <div className="min-w-0 space-y-0.5">
            <h2 className="text-sm font-semibold text-text-primary">{title}</h2>
            {description && <p className="text-sm text-text-muted">{description}</p>}
          </div>
        </div>
        {action && <div className="flex shrink-0 items-center gap-2">{action}</div>}
      </header>
      {children != null && (
        <div className={cn(flush ? 'mt-4' : 'px-5 pb-5 pt-4', contentClassName)}>{children}</div>
      )}
    </section>
  );
}
