import { StageTimeline } from '@/components/timeline/StageTimeline';
import type { StageTimelineStage } from '@/components/timeline/StageTimeline';

export type ExecutionStageId =
  'accepted' | 'searching' | 'selecting' | 'generating' | 'validating' | 'saving';

const EXECUTION_STAGES: { id: ExecutionStageId; label: string }[] = [
  { id: 'accepted', label: 'Request accepted' },
  { id: 'searching', label: 'Sections searched' },
  { id: 'selecting', label: 'Passages selected' },
  { id: 'generating', label: 'Generating' },
  { id: 'validating', label: 'Validating citations' },
  { id: 'saving', label: 'Saving' },
];

export interface StagedExecutionProps {
  /** The stage currently in progress. Ignored once `failedStage` is set. */
  currentStage: ExecutionStageId;
  /** When set, this stage (and everything after it) stops - shown as failed/pending, not active. */
  failedStage?: ExecutionStageId;
  className?: string;
}

/**
 * TASK-UX-10 (Module 37): replaces a bare spinner during agent execution
 * with staged progress ("Request accepted -> sections searched -> passages
 * selected -> generating -> validating citations -> saving"), per FR-RUN-007
 * and the "staged execution, not a bare spinner" acceptance criterion.
 * Reuses `StageTimeline` (TASK-UX-09) so the marker shapes, screen-reader
 * status words, and reduced-motion pulse handling are shared/consistent.
 */
function StagedExecution({ currentStage, failedStage, className }: StagedExecutionProps) {
  const currentIndex = EXECUTION_STAGES.findIndex((stage) => stage.id === currentStage);
  const failedIndex = failedStage
    ? EXECUTION_STAGES.findIndex((stage) => stage.id === failedStage)
    : -1;

  const stages: StageTimelineStage[] = EXECUTION_STAGES.map((stage, index) => {
    if (failedIndex !== -1) {
      if (index < failedIndex) return { ...stage, status: 'done' };
      if (index === failedIndex) return { ...stage, status: 'failed' };
      return { ...stage, status: 'pending' };
    }

    if (index < currentIndex) return { ...stage, status: 'done' };
    if (index === currentIndex) return { ...stage, status: 'active' };
    return { ...stage, status: 'pending' };
  });

  return <StageTimeline stages={stages} className={className} />;
}

export { StagedExecution, EXECUTION_STAGES };
