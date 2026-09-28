import { cn } from '@/lib/utils';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { useRealtimeConnection } from '@/app/realtime/realtime-context';
import type { RealtimeConnectionStatus } from '@/app/realtime/realtime-context';

const STATUS_LABEL: Record<RealtimeConnectionStatus, string> = {
  connecting: 'Connecting to live updates…',
  connected: 'Live updates connected',
  reconnecting: 'Reconnecting to live updates…',
  disconnected: 'Live updates disconnected - retrying',
};

const STATUS_DOT_CLASS: Record<RealtimeConnectionStatus, string> = {
  connecting: 'bg-text-muted',
  connected: 'bg-success',
  reconnecting: 'bg-warning',
  disconnected: 'bg-danger',
};

/**
 * TASK-SIG-05: header indicator for the realtime (SignalR) connection that
 * drives live knowledge-file/agent-run/dashboard updates - mirrors
 * `HealthIndicator`'s color + tooltip convention (never color alone).
 */
export function RealtimeConnectionIndicator() {
  const { status } = useRealtimeConnection();

  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <span
          className="inline-flex items-center gap-2 rounded-control px-2 py-1"
          aria-label={STATUS_LABEL[status]}
        >
          <span
            className={cn('size-2 rounded-full', STATUS_DOT_CLASS[status])}
            data-testid="realtime-indicator-dot"
            data-status={status}
            aria-hidden="true"
          />
          <span className="sr-only">{STATUS_LABEL[status]}</span>
        </span>
      </TooltipTrigger>
      <TooltipContent side="bottom">{STATUS_LABEL[status]}</TooltipContent>
    </Tooltip>
  );
}
