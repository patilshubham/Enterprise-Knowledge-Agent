import { motion } from 'motion/react';
import { CheckIcon, CircleIcon, XIcon } from 'lucide-react';

import { cn } from '@/lib/utils';
import { useReducedMotionPreference } from '@/lib/motion/useReducedMotionPreference';
import { motionDurationsSeconds, motionEasings } from '@/lib/motion/presets';

export type StageStatus = 'done' | 'active' | 'pending' | 'failed';

export interface StageTimelineStage {
  id: string;
  label: string;
  status: StageStatus;
  /** e.g. "Embedding 42/128". */
  detail?: string;
  /** Preformatted display timestamp (e.g. "10:42:03 AM"). */
  timestamp?: string;
}

export interface StageTimelineProps {
  stages: StageTimelineStage[];
  className?: string;
}

const STATUS_TEXT: Record<StageStatus, string> = {
  done: 'Completed',
  active: 'In progress',
  pending: 'Pending',
  failed: 'Failed',
};

function StageMarker({ status }: { status: StageStatus }) {
  const prefersReducedMotion = useReducedMotionPreference();

  if (status === 'done') {
    return (
      <span className="flex size-6 shrink-0 items-center justify-center rounded-full bg-success text-white">
        <CheckIcon aria-hidden="true" className="size-3.5" />
      </span>
    );
  }

  if (status === 'failed') {
    return (
      <span className="flex size-6 shrink-0 items-center justify-center rounded-full bg-danger text-white">
        <XIcon aria-hidden="true" className="size-3.5" />
      </span>
    );
  }

  if (status === 'active') {
    return (
      <span className="relative flex size-6 shrink-0 items-center justify-center">
        {!prefersReducedMotion && (
          <motion.span
            aria-hidden="true"
            className="absolute inset-0 rounded-full bg-processing/40"
            animate={{ opacity: [0.6, 0, 0.6], scale: [1, 1.35, 1] }}
            transition={{
              duration: motionDurationsSeconds.statusTransition * 3,
              ease: motionEasings.standard,
              repeat: Infinity,
            }}
          />
        )}
        <span className="relative flex size-6 items-center justify-center rounded-full bg-processing text-white">
          <CircleIcon aria-hidden="true" className="size-2.5 fill-current" />
        </span>
      </span>
    );
  }

  return (
    <span className="flex size-6 shrink-0 items-center justify-center rounded-full border-2 border-border text-transparent">
      <CircleIcon aria-hidden="true" className="size-2.5" />
    </span>
  );
}

/**
 * TASK-UX-09 (Module 37): reusable vertical stage-timeline for processing
 * jobs and agent runs (Uploaded -> Validated -> Parsed -> Chunked ->
 * Embedding N/M -> Indexed -> Ready, or the equivalent run stages). Each
 * stage's state is conveyed by marker shape (check/pulsing dot/empty
 * circle/x) plus a visible label and a screen-reader-only status word -
 * never by color alone. The active stage's pulse is the only motion here,
 * and is suppressed entirely under `prefers-reduced-motion`.
 */
function StageTimeline({ stages, className }: StageTimelineProps) {
  return (
    <ol className={cn('flex flex-col', className)}>
      {stages.map((stage, index) => (
        <li key={stage.id} className="flex gap-3">
          <div className="flex flex-col items-center">
            <StageMarker status={stage.status} />
            {index < stages.length - 1 && (
              <span
                aria-hidden="true"
                className={cn('w-px flex-1', stage.status === 'done' ? 'bg-success' : 'bg-border')}
              />
            )}
          </div>
          <div className="pb-6">
            <p className="text-sm font-medium text-text-primary">
              {stage.label}
              <span className="sr-only">{` - ${STATUS_TEXT[stage.status]}`}</span>
            </p>
            {stage.detail && <p className="text-xs text-text-muted">{stage.detail}</p>}
            {stage.timestamp && <p className="text-xs text-text-muted">{stage.timestamp}</p>}
          </div>
        </li>
      ))}
    </ol>
  );
}

export { StageTimeline };
