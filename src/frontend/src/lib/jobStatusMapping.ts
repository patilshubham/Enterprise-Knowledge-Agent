import type { StageTimelineStage } from '@/components/timeline/StageTimeline';
import type { StatusKind } from '@/components/status/status-tokens';
import type { JobStatus, ProcessingStage } from '@/api/processingJobs';
import type { RunStage } from '@/api/runs';

/**
 * TASK-PRDSH-01/02 + TASK-RDSH-01/02: maps the shared `JobStatus` (queued
 * processing job or agent run) onto the app's fixed `StatusKind` vocabulary
 * for `StatusBadge` - never invents new colors per feature module.
 */
export function jobStatusToStatusKind(status: JobStatus): StatusKind {
  switch (status) {
    case 'Queued':
      return 'queued';
    case 'Running':
      return 'processing';
    case 'Completed':
      return 'ready';
    case 'Failed':
      return 'failed';
    case 'Cancelled':
      return 'disabled';
    default:
      return 'queued';
  }
}

const PROCESSING_STAGE_ORDER: ProcessingStage[] = [
  'Queued',
  'Downloading',
  'Validating',
  'Parsing',
  'Chunking',
  'GeneratingEmbeddings',
  'SavingVectors',
  'Finalizing',
  'Completed',
];

const PROCESSING_STAGE_LABELS: Record<ProcessingStage, string> = {
  Queued: 'Queued',
  Downloading: 'Downloading',
  Validating: 'Validating',
  Parsing: 'Parsing',
  Chunking: 'Chunking',
  GeneratingEmbeddings: 'Generating embeddings',
  SavingVectors: 'Saving vectors',
  Finalizing: 'Finalizing',
  Completed: 'Completed',
};

/** TASK-PRDSH-02: builds the fixed 9-stage processing timeline, marking done/active/failed/pending relative to the job's current stage+status - mirrors `StagedExecution`'s established mapping pattern. */
export function buildProcessingStages(
  stage: ProcessingStage,
  status: JobStatus,
  progress: number,
): StageTimelineStage[] {
  const currentIndex = PROCESSING_STAGE_ORDER.indexOf(stage);
  const isTerminalFailure = status === 'Failed' || status === 'Cancelled';

  return PROCESSING_STAGE_ORDER.map((stageId, index) => {
    let timelineStatus: StageTimelineStage['status'];
    if (index === currentIndex && isTerminalFailure) {
      timelineStatus = 'failed';
    } else if (index < currentIndex || (index === currentIndex && status === 'Completed')) {
      timelineStatus = 'done';
    } else if (index === currentIndex) {
      timelineStatus = 'active';
    } else {
      timelineStatus = 'pending';
    }

    return {
      id: stageId,
      label: PROCESSING_STAGE_LABELS[stageId],
      status: timelineStatus,
      detail: index === currentIndex && timelineStatus === 'active' ? `${progress}%` : undefined,
    };
  });
}

const RUN_STAGE_ORDER: RunStage[] = [
  'Queued',
  'Preparing',
  'Retrieving',
  'BuildingContext',
  'Generating',
  'Validating',
  'Saving',
  'Completed',
];

const RUN_STAGE_LABELS: Record<RunStage, string> = {
  Queued: 'Queued',
  Preparing: 'Preparing',
  Retrieving: 'Retrieving',
  BuildingContext: 'Building context',
  Generating: 'Generating',
  Validating: 'Validating',
  Saving: 'Saving',
  Completed: 'Completed',
  Failed: 'Failed',
  Cancelled: 'Cancelled',
};

/** TASK-RDSH-02: builds the fixed 8-stage run timeline (Failed/Cancelled are terminal markers applied to the last active stage, not steps of their own). */
export function buildRunStages(
  stage: RunStage,
  status: JobStatus,
  progress: number,
): StageTimelineStage[] {
  const isTerminalFailure = status === 'Failed' || status === 'Cancelled';
  // `stage` itself can be 'Failed'/'Cancelled' (the run's own terminal stage
  // value) - resolve to the furthest non-terminal stage reached so the
  // timeline still shows meaningful progress instead of index -1.
  const effectiveStage = stage === 'Failed' || stage === 'Cancelled' ? 'Saving' : stage;
  const currentIndex = RUN_STAGE_ORDER.indexOf(effectiveStage);

  return RUN_STAGE_ORDER.map((stageId, index) => {
    let timelineStatus: StageTimelineStage['status'];
    if (index === currentIndex && isTerminalFailure) {
      timelineStatus = 'failed';
    } else if (index < currentIndex || (index === currentIndex && status === 'Completed')) {
      timelineStatus = 'done';
    } else if (index === currentIndex) {
      timelineStatus = 'active';
    } else {
      timelineStatus = 'pending';
    }

    return {
      id: stageId,
      label: RUN_STAGE_LABELS[stageId],
      status: timelineStatus,
      detail: index === currentIndex && timelineStatus === 'active' ? `${progress}%` : undefined,
    };
  });
}
