import { cn } from '@/lib/utils';
import { Badge } from '@/components/ui/badge';
import { useReducedMotionPreference } from '@/lib/motion/useReducedMotionPreference';
import { STATUS_DEFINITIONS } from '@/components/status/status-tokens';
import type { StatusKind } from '@/components/status/status-tokens';
import { AlertTriangleIcon } from 'lucide-react';

export interface StatusBadgeProps {
  status: StatusKind;
  /** Overrides the default label (e.g. a domain-specific phrase for "ready"). */
  label?: string;
  className?: string;
}

/**
 * TASK-UX-09 (Module 37): renders a status as color + icon + text label
 * together - never color alone. The `processing` status spins its icon
 * unless the user prefers reduced motion, in which case it renders static.
 */
function StatusBadge({ status, label, className }: StatusBadgeProps) {
  const prefersReducedMotion = useReducedMotionPreference();
  // Defensive fallback: a caller passing a status value that isn't (or is no
  // longer) a key of STATUS_DEFINITIONS - e.g. a backend enum value the
  // frontend's mapping table hasn't been updated for yet - must never crash
  // the whole app (this previously threw "Cannot read properties of
  // undefined (reading 'icon')" and took down the entire page).
  const definition = STATUS_DEFINITIONS[status] ?? {
    label: label ?? 'Unknown',
    icon: AlertTriangleIcon,
    badgeVariant: 'outline' as const,
  };
  const Icon = definition.icon;

  return (
    <Badge variant={definition.badgeVariant} className={cn('gap-1.5', className)}>
      <Icon
        aria-hidden="true"
        className={cn(definition.spin && !prefersReducedMotion && 'animate-spin')}
      />
      {label ?? definition.label}
    </Badge>
  );
}

export { StatusBadge };
