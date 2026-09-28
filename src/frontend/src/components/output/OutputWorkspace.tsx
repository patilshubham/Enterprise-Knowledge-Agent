import { useState } from 'react';
import type { ReactNode } from 'react';
import { motion } from 'motion/react';

import { SafeMarkdown } from '@/components/markdown/SafeMarkdown';
import { CodeEditor } from '@/components/editor/CodeEditor';
import { ResizableHandle, ResizablePanel, ResizablePanelGroup } from '@/components/ui/resizable';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { EvidenceDrawer } from '@/components/evidence/EvidenceDrawer';
import type { EvidenceSource } from '@/components/evidence/EvidenceDrawer';
import { StagedExecution } from '@/components/output/StagedExecution';
import type { ExecutionStageId } from '@/components/output/StagedExecution';
import { useReducedMotionPreference } from '@/lib/motion/useReducedMotionPreference';
import { drawerTransition } from '@/lib/motion/presets';

export type OutputWorkspaceMode = 'preview' | 'source' | 'split';

export interface OutputExecutionState {
  currentStage: ExecutionStageId;
  failedStage?: ExecutionStageId;
}

export interface OutputWorkspaceProps {
  /** Markdown content of the generated output. Ignored while `execution` is set. */
  content: string;
  /** Present while the run is still executing - staged progress replaces the content. */
  execution?: OutputExecutionState;
  sources: EvidenceSource[];
  /**
   * Rendered in the toolbar, e.g. an admin-only "Run details" button wired to
   * its own `DetailDrawer` - callers gate this to authorized roles
   * (`useCapabilities`); omitted entirely when not provided.
   */
  runDetailsAction?: ReactNode;
  className?: string;
}

/**
 * TASK-UX-10 (Module 37): the evidence-first Markdown workspace shell -
 * Preview / Markdown Source / Split modes rendering via the hardened
 * `<SafeMarkdown>` (TASK-MDR-01, raw HTML disabled) on a resizable split
 * (TASK-UX-07), with citation clicks opening the `EvidenceDrawer`. While
 * `execution` is set, `StagedExecution` (TASK-UX-09's `StageTimeline`)
 * replaces the output entirely instead of a bare spinner; on completion the
 * output fades in (<=300ms), skipped entirely under
 * `prefers-reduced-motion` (content just appears, no animation).
 */
function OutputWorkspace({
  content,
  execution,
  sources,
  runDetailsAction,
  className,
}: OutputWorkspaceProps) {
  const [mode, setMode] = useState<OutputWorkspaceMode>('preview');
  const [activeCitationId, setActiveCitationId] = useState<string | undefined>(undefined);
  const [evidenceOpen, setEvidenceOpen] = useState(false);
  const prefersReducedMotion = useReducedMotionPreference();

  const handleCitationClick = (footnoteId: string) => {
    setActiveCitationId(footnoteId);
    setEvidenceOpen(true);
  };

  const preview = (
    <motion.div
      key={execution ? 'executing' : 'ready'}
      initial={prefersReducedMotion ? false : { opacity: 0 }}
      animate={{ opacity: 1 }}
      transition={drawerTransition}
    >
      <SafeMarkdown content={content} onCitationClick={handleCitationClick} />
    </motion.div>
  );

  const source = (
    <CodeEditor
      value={content}
      onChange={() => {}}
      language="markdown"
      readOnly
      ariaLabel="Markdown source"
    />
  );

  return (
    <div className={className}>
      <div className="mb-3 flex items-center justify-between gap-2">
        <Tabs value={mode} onValueChange={(value) => setMode(value as OutputWorkspaceMode)}>
          <TabsList>
            <TabsTrigger value="preview">Preview</TabsTrigger>
            <TabsTrigger value="source">Markdown Source</TabsTrigger>
            <TabsTrigger value="split">Split</TabsTrigger>
          </TabsList>
        </Tabs>
        {runDetailsAction}
      </div>

      {execution ? (
        <StagedExecution
          currentStage={execution.currentStage}
          failedStage={execution.failedStage}
        />
      ) : (
        <>
          {mode === 'preview' && preview}
          {mode === 'source' && <div className="h-96">{source}</div>}
          {mode === 'split' && (
            <ResizablePanelGroup orientation="horizontal" className="h-96">
              <ResizablePanel defaultSize={50} minSize={20}>
                {source}
              </ResizablePanel>
              <ResizableHandle />
              <ResizablePanel defaultSize={50} minSize={20}>
                <div className="h-full overflow-y-auto">{preview}</div>
              </ResizablePanel>
            </ResizablePanelGroup>
          )}
        </>
      )}

      <EvidenceDrawer
        open={evidenceOpen}
        onOpenChange={setEvidenceOpen}
        sources={sources}
        activeSourceId={activeCitationId}
      />
    </div>
  );
}

export { OutputWorkspace };
