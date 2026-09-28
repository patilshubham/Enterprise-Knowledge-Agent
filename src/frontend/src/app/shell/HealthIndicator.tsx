import { cn } from '@/lib/utils';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { useReadiness } from './useReadiness';

const READINESS_LABEL: Record<string, string> = {
  healthy: 'All systems operational',
  degraded: 'Degraded - some dependencies are unhealthy',
  unhealthy: 'Unhealthy - service is not ready',
  unknown: 'Health status unknown',
};

const READINESS_DOT_CLASS: Record<string, string> = {
  healthy: 'bg-success',
  degraded: 'bg-warning',
  unhealthy: 'bg-danger',
  unknown: 'bg-text-muted',
};

/**
 * TASK-UX-04: header health indicator fed by `/health/ready` (module 31
 * eventually enriches this with per-dependency detail). Degrades gracefully
 * to a neutral "unknown" state rather than erroring the shell when the
 * readiness probe itself is unreachable.
 */
export function HealthIndicator() {
  const readiness = useReadiness();

  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <span
          className="inline-flex items-center gap-2 rounded-control px-2 py-1"
          aria-label={READINESS_LABEL[readiness]}
        >
          <span
            className={cn('size-2 rounded-full', READINESS_DOT_CLASS[readiness])}
            data-testid="health-indicator-dot"
            data-readiness={readiness}
            aria-hidden="true"
          />
          <span className="sr-only">{READINESS_LABEL[readiness]}</span>
        </span>
      </TooltipTrigger>
      <TooltipContent side="bottom">{READINESS_LABEL[readiness]}</TooltipContent>
    </Tooltip>
  );
}
