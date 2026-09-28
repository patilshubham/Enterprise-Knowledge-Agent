import {
  AlertTriangleIcon,
  BanIcon,
  CheckCircle2Icon,
  ClockIcon,
  Loader2Icon,
  XCircleIcon,
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import type { VariantProps } from 'class-variance-authority';

import type { badgeVariants } from '@/components/ui/badge-variants';

/**
 * TASK-UX-09 (Module 37): the app's one accessible status vocabulary - every
 * status is always color + icon + text label together (ASDD §21.3/D15),
 * never color alone. Reused by `StatusBadge`, `StageTimeline`, and
 * `DetailDrawer`; feature modules should map their own domain statuses onto
 * this small fixed set instead of inventing new colors/icons.
 */
export type StatusKind = 'queued' | 'processing' | 'ready' | 'warning' | 'failed' | 'disabled';

export interface StatusDefinition {
  label: string;
  icon: LucideIcon;
  badgeVariant: NonNullable<VariantProps<typeof badgeVariants>['variant']>;
  /** Only `processing` spins its icon; every other status is fully static. */
  spin?: boolean;
}

export const STATUS_DEFINITIONS: Record<StatusKind, StatusDefinition> = {
  queued: { label: 'Queued', icon: ClockIcon, badgeVariant: 'secondary' },
  processing: { label: 'Processing', icon: Loader2Icon, badgeVariant: 'processing', spin: true },
  ready: { label: 'Ready', icon: CheckCircle2Icon, badgeVariant: 'success' },
  warning: { label: 'Warning', icon: AlertTriangleIcon, badgeVariant: 'warning' },
  failed: { label: 'Failed', icon: XCircleIcon, badgeVariant: 'destructive' },
  disabled: { label: 'Disabled', icon: BanIcon, badgeVariant: 'outline' },
};
