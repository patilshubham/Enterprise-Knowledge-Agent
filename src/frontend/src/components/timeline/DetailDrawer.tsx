import type { ReactNode } from 'react';

import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/sheet';
import { Button } from '@/components/ui/button';
import { StatusBadge } from '@/components/status/StatusBadge';
import { StageTimeline } from '@/components/timeline/StageTimeline';
import type { StatusKind } from '@/components/status/status-tokens';
import type { StageTimelineStage } from '@/components/timeline/StageTimeline';

export interface DetailDrawerProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  status: StatusKind;
  statusLabel?: string;
  stages: StageTimelineStage[];
  /** Preformatted display text (e.g. "Oct 3, 10:42:03 AM"). */
  startedAt?: string;
  /** Preformatted display text (e.g. "2m 14s"). */
  elapsed?: string;
  initiator?: string;
  correlationId?: string;
  /** Rendered as a footer action only when provided - callers gate this to authorized roles. */
  onRetry?: () => void;
  retryLabel?: string;
  children?: ReactNode;
}

/**
 * TASK-UX-09 (Module 37): shared detail side-drawer for a processing job or
 * agent run - status, current stage/progress via `StageTimeline`, timings,
 * initiator, and correlation ID (for support/debugging). Retry is only
 * rendered when `onRetry` is supplied; gating to admins/platform roles is
 * the caller's responsibility (`useCapabilities`).
 */
function DetailDrawer({
  open,
  onOpenChange,
  title,
  status,
  statusLabel,
  stages,
  startedAt,
  elapsed,
  initiator,
  correlationId,
  onRetry,
  retryLabel = 'Retry',
  children,
}: DetailDrawerProps) {
  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent side="right" className="w-full sm:max-w-md">
        <SheetHeader>
          <SheetTitle>{title}</SheetTitle>
          <SheetDescription className="sr-only">Processing detail drawer</SheetDescription>
          <StatusBadge status={status} label={statusLabel} className="w-fit" />
        </SheetHeader>

        <div className="flex-1 space-y-6 overflow-y-auto">
          {(startedAt || elapsed || initiator || correlationId) && (
            <dl className="grid grid-cols-2 gap-x-4 gap-y-3 text-sm">
              {startedAt && (
                <div>
                  <dt className="text-xs text-text-muted">Started</dt>
                  <dd className="text-text-primary">{startedAt}</dd>
                </div>
              )}
              {elapsed && (
                <div>
                  <dt className="text-xs text-text-muted">Elapsed</dt>
                  <dd className="text-text-primary">{elapsed}</dd>
                </div>
              )}
              {initiator && (
                <div>
                  <dt className="text-xs text-text-muted">Initiator</dt>
                  <dd className="text-text-primary">{initiator}</dd>
                </div>
              )}
              {correlationId && (
                <div>
                  <dt className="text-xs text-text-muted">Correlation ID</dt>
                  <dd className="font-mono text-xs break-all text-text-primary">{correlationId}</dd>
                </div>
              )}
            </dl>
          )}

          <StageTimeline stages={stages} />

          {children}
        </div>

        {onRetry && (
          <SheetFooter>
            <Button variant="outline" onClick={onRetry}>
              {retryLabel}
            </Button>
          </SheetFooter>
        )}
      </SheetContent>
    </Sheet>
  );
}

export { DetailDrawer };
