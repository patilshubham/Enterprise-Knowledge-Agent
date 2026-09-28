import {
  AlertTriangleIcon,
  BanIcon,
  CheckCircle2Icon,
  ClockIcon,
  Loader2Icon,
  Trash2Icon,
  XCircleIcon,
} from 'lucide-react';
import type {
  DashboardAttentionItem,
  JobStatusCounts,
  KnowledgeFileCounts,
} from '@/api/dashboards';
import { MetricTile, MetricTileGrid } from '@/components/patterns/MetricTile';

/**
 * TASK-PDSH-02/TASK-ADSH-02: small presentational building blocks shared by
 * the platform and application dashboards - both render the same
 * `JobStatusCounts`/`KnowledgeFileCounts`/`DashboardAttentionItem` shapes
 * (see `src/application/Dashboards/DashboardDtos.cs`), just scoped
 * differently (platform-wide vs. one application).
 *
 * Redesign: counts now render as icon-chipped `MetricTile`s (zeros dim so the
 * actionable, non-zero numbers stand out) rather than bare label/number pairs.
 */
export function Metric({ label, value }: { label: string; value: number }) {
  return (
    <div className="flex flex-col gap-0.5">
      <span className="text-xs text-text-muted">{label}</span>
      <span className="text-lg font-semibold tabular-nums">{value}</span>
    </div>
  );
}

export function JobStatusMetrics({ counts }: { counts: JobStatusCounts }) {
  // TASK-PDSH-02/TASK-ADSH-02: this renders inside a half-width SectionCard
  // (paired via `grid lg:grid-cols-2` on both the platform and application
  // dashboards), which tops out around ~520-570px even on wide viewports
  // since the page content is capped at `max-w-6xl`. `lg:grid-cols-5` was
  // triggering based on *viewport* width, not the card's actual (halved)
  // width, squeezing all 5 icon+value+label tiles into ~87px each. 3
  // columns (wrapping 5 items to 3+2) keeps tiles as roomy as the sibling
  // "Applications" card at the same width.
  return (
    <MetricTileGrid columns={3}>
      <MetricTile label="Queued" value={counts.queued} icon={ClockIcon} tone="neutral" />
      <MetricTile label="Running" value={counts.running} icon={Loader2Icon} tone="processing" />
      <MetricTile label="Completed" value={counts.completed} icon={CheckCircle2Icon} tone="success" />
      <MetricTile label="Failed" value={counts.failed} icon={XCircleIcon} tone="danger" />
      <MetricTile label="Cancelled" value={counts.cancelled} icon={BanIcon} tone="neutral" />
    </MetricTileGrid>
  );
}

export function KnowledgeFileMetrics({ counts }: { counts: KnowledgeFileCounts }) {
  return (
    <MetricTileGrid columns={4}>
      <MetricTile label="Queued" value={counts.queued} icon={ClockIcon} tone="neutral" />
      <MetricTile label="Processing" value={counts.processing} icon={Loader2Icon} tone="processing" />
      <MetricTile label="Ready" value={counts.ready} icon={CheckCircle2Icon} tone="success" />
      <MetricTile label="Failed" value={counts.failed} icon={XCircleIcon} tone="danger" />
      <MetricTile label="Disabled" value={counts.disabled} icon={BanIcon} tone="neutral" />
      <MetricTile label="Deleting" value={counts.deleting} icon={Loader2Icon} tone="warning" />
      <MetricTile label="Deleted" value={counts.deleted} icon={Trash2Icon} tone="neutral" />
    </MetricTileGrid>
  );
}

/** Human-readable labels for `DashboardAttentionItem.kind` - falls back to the server-supplied `message` for any kind not listed here. */
const ATTENTION_ITEM_LABELS: Record<string, string> = {
  ProcessingJobsFailed: 'Failed processing jobs',
  AgentRunsFailed: 'Failed agent runs',
  WorkersStale: 'Stale workers',
  AoaiUnavailable: 'Azure OpenAI unavailable',
  DeadLetterMessages: 'Dead-letter messages',
};

export function AttentionItemRow({ item }: { item: DashboardAttentionItem }) {
  return (
    <div className="flex items-center gap-3 rounded-card border border-warning/30 bg-warning/10 px-4 py-3 text-sm shadow-card">
      <span
        className="icon-chip size-9 shrink-0"
        style={{ ['--chip-color' as string]: 'var(--warning)' }}
        aria-hidden="true"
      >
        <AlertTriangleIcon className="size-[18px]" />
      </span>
      <span className="flex-1 font-medium text-text-primary">
        {ATTENTION_ITEM_LABELS[item.kind] ?? item.message}
      </span>
      <span className="rounded-full bg-warning/15 px-2.5 py-0.5 text-sm font-semibold tabular-nums text-warning">
        {item.count}
      </span>
    </div>
  );
}

