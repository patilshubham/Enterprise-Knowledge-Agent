import { Fragment } from 'react';
import type { LucideIcon } from 'lucide-react';
import { ChevronDown, ChevronRight } from 'lucide-react';
import { Link } from 'react-router-dom';
import { cn } from '@/lib/utils';
import { StatusBadge } from '@/components/status/StatusBadge';
import type { StatusKind } from '@/components/status/status-tokens';
import { chipStyle } from './tone';
import type { Tone } from './tone';

export interface PipelineStage {
  key: string;
  label: string;
  icon: LucideIcon;
  /** Where clicking the stage navigates. */
  to: string;
  /** Headline number for the stage (e.g. files ready, agents, runs). */
  value: number | string;
  valueLabel: string;
  tone?: Tone;
  /** Optional accessible status pill (ready / processing / failed / …). */
  status?: StatusKind;
  statusLabel?: string;
  /** Small helper line under the value. */
  hint?: string;
}

/**
 * Redesign flagship: renders the application's real pipeline
 * (Knowledge → Processing → Agents → Runs → Outputs) as a connected sequence
 * of stage cards with directional connectors, so the overview reads as one
 * flowing process instead of a stack of unrelated stat cards. Horizontal on
 * wide screens, vertical on narrow ones. Every stage is a link into its page.
 */
export function WorkflowPipeline({ stages }: { stages: PipelineStage[] }) {
  return (
    <div className="flex flex-col gap-2 lg:flex-row lg:items-stretch">
      {stages.map((stage, index) => {
        const Icon = stage.icon;
        const tone: Tone = stage.tone ?? 'accent';
        return (
          <Fragment key={stage.key}>
            <Link
              to={stage.to}
              className={cn(
                'lift group flex flex-1 flex-col gap-3 rounded-card border border-border bg-surface/70 p-4',
                'hover:border-border-strong hover:bg-surface-hover',
              )}
            >
              <div className="flex items-center justify-between gap-2">
                <span className="flex min-w-0 items-center gap-2">
                  <span className="icon-chip size-8 shrink-0" style={chipStyle(tone)} aria-hidden="true">
                    <Icon className="size-4" />
                  </span>
                  <span className="truncate text-sm font-medium text-text-secondary">
                    {stage.label}
                  </span>
                </span>
                {stage.status && (
                  <StatusBadge status={stage.status} label={stage.statusLabel} />
                )}
              </div>
              <div>
                <div className="text-2xl font-semibold leading-none tabular-nums text-text-primary">
                  {stage.value}
                </div>
                <div className="mt-1 text-xs text-text-muted">{stage.valueLabel}</div>
              </div>
              <div className="mt-auto flex items-center gap-1 text-xs text-text-muted">
                {stage.hint ? (
                  <span className="truncate">{stage.hint}</span>
                ) : (
                  <span className="text-text-muted/70">Open</span>
                )}
                <ChevronRight className="size-3.5 shrink-0 opacity-0 transition-opacity group-hover:opacity-100" aria-hidden="true" />
              </div>
            </Link>

            {index < stages.length - 1 && (
              <div
                className="flex items-center justify-center text-text-muted lg:px-0.5"
                aria-hidden="true"
              >
                <ChevronRight className="hidden size-4 lg:block" />
                <ChevronDown className="size-4 lg:hidden" />
              </div>
            )}
          </Fragment>
        );
      })}
    </div>
  );
}
