import { useEffect, useRef, useState } from 'react';

import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/sheet';
import { cn } from '@/lib/utils';

export interface EvidenceSource {
  /** Matches the footnote id `SafeMarkdown`'s `onCitationClick` reports (e.g. "1" for `[^1]`). */
  id: string;
  fileName: string;
  /** e.g. "Onboarding Guide > Access Requests". */
  headingPath?: string;
  passage: string;
  /**
   * TASK-CIT-04 (deleted-source citations handled gracefully): when true,
   * the passage is no longer available and a safe fallback is shown instead.
   */
  deleted?: boolean;
}

export interface EvidenceDrawerProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  sources: EvidenceSource[];
  /** The source to scroll to and briefly highlight, e.g. the citation just clicked. */
  activeSourceId?: string;
}

const HIGHLIGHT_DURATION_MS = 400;

/**
 * TASK-UX-10 (Module 37): the evidence panel/drawer that a citation click
 * opens - lists every source used, scrolls to and briefly highlights the
 * source matching `activeSourceId`, and shows filename + heading path per
 * FR-CIT-003 / the "citation -> evidence interaction" acceptance criterion.
 * The highlight is a brief, non-decorative background flash (<=500ms) used
 * purely to draw attention to WHERE the drawer scrolled to, not for effect.
 */
function EvidenceDrawer({ open, onOpenChange, sources, activeSourceId }: EvidenceDrawerProps) {
  const itemRefs = useRef(new Map<string, HTMLLIElement>());
  const [highlightedId, setHighlightedId] = useState<string | undefined>(undefined);
  const [trackedSourceId, setTrackedSourceId] = useState<string | undefined>(undefined);

  // Deriving state during render (React's blessed pattern for "reset/update
  // state when a prop changes") instead of a synchronous setState-in-effect,
  // so a fresh `activeSourceId` is highlighted immediately on this render.
  if (open && activeSourceId && activeSourceId !== trackedSourceId) {
    setTrackedSourceId(activeSourceId);
    setHighlightedId(activeSourceId);
  }

  // Radix's Sheet/Dialog content mounts into its portal a commit after this
  // component's own effects first run (its `Presence` state settles via its
  // own layout effect before the children are actually rendered), so a
  // plain `useEffect` here can fire before the target `<li>`'s ref exists.
  // Scrolling from the ref callback itself (below) covers that initial-open
  // race; this effect covers subsequent `activeSourceId` changes while the
  // drawer is already mounted (ref already present by then).
  useEffect(() => {
    if (!open || !activeSourceId) return;
    itemRefs.current.get(activeSourceId)?.scrollIntoView({ block: 'center' });
  }, [open, activeSourceId]);

  useEffect(() => {
    if (!highlightedId) return;
    const timeout = setTimeout(() => setHighlightedId(undefined), HIGHLIGHT_DURATION_MS);
    return () => clearTimeout(timeout);
  }, [highlightedId]);

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent side="right" className="w-full sm:max-w-md">
        <SheetHeader>
          <SheetTitle>Evidence</SheetTitle>
          <SheetDescription>Sources used to generate this output.</SheetDescription>
        </SheetHeader>

        <ol className="flex-1 space-y-3 overflow-y-auto">
          {sources.map((source) => (
            <li
              key={source.id}
              ref={(node) => {
                if (node) {
                  itemRefs.current.set(source.id, node);
                  if (open && source.id === activeSourceId) {
                    node.scrollIntoView({ block: 'center' });
                  }
                } else {
                  itemRefs.current.delete(source.id);
                }
              }}
              data-testid={`evidence-source-${source.id}`}
              className={cn(
                'rounded-md border border-border p-3 transition-colors',
                highlightedId === source.id && 'bg-accent/15 border-accent',
              )}
            >
              <p className="text-sm font-medium text-text-primary">{source.fileName}</p>
              {source.headingPath && (
                <p className="text-xs text-text-muted">{source.headingPath}</p>
              )}
              {source.deleted ? (
                <p className="mt-1 text-sm text-text-muted italic">
                  This source is no longer available.
                </p>
              ) : (
                // The passage is the exact excerpt the answer was grounded in - set it
                // apart from the muted filename/heading metadata above with a distinct
                // "highlighter" background (warning/amber, never reused elsewhere in this
                // drawer) instead of plain secondary text, so it reads as the specific
                // relevant span rather than more metadata.
                <p className="mt-1 rounded-md border-l-2 border-warning bg-warning/10 px-2 py-1.5 text-sm text-text-primary">
                  {source.passage}
                </p>
              )}
            </li>
          ))}
        </ol>
      </SheetContent>
    </Sheet>
  );
}

export { EvidenceDrawer };
