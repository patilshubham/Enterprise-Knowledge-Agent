import type { LucideIcon } from 'lucide-react';
import { Link } from 'react-router-dom';
import { cn } from '@/lib/utils';
import { chipStyle } from './tone';
import type { Tone } from './tone';

export interface MetricTileProps {
  label: string;
  value: number | string;
  icon?: LucideIcon;
  tone?: Tone;
  /** Small helper text under the value (e.g. "3 need attention"). */
  hint?: string;
  /** Renders the tile as a link. */
  to?: string;
  /** When true and the numeric value is 0, the tile is visually de-emphasized. */
  dimWhenZero?: boolean;
  className?: string;
}

/**
 * Redesign: a single metric presented as a compact, scannable tile - an icon
 * chip, a large value, and a label - instead of a bare "label: number" pair.
 * Zero values dim by default so non-zero (actionable) numbers stand out. Wrap
 * several in a responsive grid via `MetricTileGrid`.
 */
export function MetricTile({
  label,
  value,
  icon: Icon,
  tone = 'accent',
  hint,
  to,
  dimWhenZero = true,
  className,
}: MetricTileProps) {
  const isZero = typeof value === 'number' && value === 0;
  const dim = dimWhenZero && isZero;

  const inner = (
    <>
      {Icon && (
        <span
          className={cn('icon-chip size-9 shrink-0 transition-opacity', dim && 'opacity-45')}
          style={chipStyle(tone)}
          aria-hidden="true"
        >
          <Icon className="size-[18px]" />
        </span>
      )}
      <div className="min-w-0">
        <div
          className={cn(
            'text-xl font-semibold leading-tight tabular-nums text-text-primary',
            dim && 'text-text-muted',
          )}
        >
          {value}
        </div>
        <div className="text-[11px] font-medium leading-tight text-text-muted">{label}</div>
        {hint && <div className="mt-0.5 text-xs leading-tight text-text-secondary">{hint}</div>}
      </div>
    </>
  );

  const base = cn(
    'flex items-center gap-3 rounded-control border border-border bg-surface/60 px-3.5 py-3',
    to && 'lift hover:border-border-strong hover:bg-surface-hover',
    className,
  );

  if (to) {
    return (
      <Link to={to} className={base}>
        {inner}
      </Link>
    );
  }
  return <div className={base}>{inner}</div>;
}

/** Responsive grid wrapper for a set of `MetricTile`s. */
export function MetricTileGrid({
  children,
  className,
  columns = 4,
}: {
  children: React.ReactNode;
  className?: string;
  columns?: 3 | 4 | 5 | 6;
}) {
  const cols: Record<number, string> = {
    3: 'sm:grid-cols-2 lg:grid-cols-3',
    4: 'sm:grid-cols-2 lg:grid-cols-4',
    5: 'sm:grid-cols-3 lg:grid-cols-5',
    6: 'sm:grid-cols-3 lg:grid-cols-6',
  };
  return <div className={cn('grid grid-cols-2 gap-3', cols[columns], className)}>{children}</div>;
}
