import type { ComponentProps } from 'react';
import { GripVertical } from 'lucide-react';
import { Group, Panel, Separator } from 'react-resizable-panels';

import { cn } from '@/lib/utils';

/**
 * TASK-UX-07 (Module 37): shared resizable split-panel primitive, used by the
 * agent prompt editor/settings split, the Markdown source/preview split, and
 * the output/evidence split.
 *
 * `react-resizable-panels` v4's API is `Group`/`Panel`/`Separator` (not the
 * older `PanelGroup`/`PanelResizeHandle` names). To persist panel sizes
 * across reloads, pair `ResizablePanelGroup` with the library's own
 * `useDefaultLayout({ id, storage: localStorage })` hook and spread its
 * `defaultLayout`/`onLayoutChanged` result onto the group:
 *
 * ```tsx
 * const layout = useDefaultLayout({ id: 'agent-editor', storage: localStorage });
 * <ResizablePanelGroup orientation="horizontal" {...layout}>
 *   <ResizablePanel defaultSize={65} minSize={30}>...</ResizablePanel>
 *   <ResizableHandle />
 *   <ResizablePanel defaultSize={35} minSize={20}>...</ResizablePanel>
 * </ResizablePanelGroup>
 * ```
 */
function ResizablePanelGroup({ className, ...props }: ComponentProps<typeof Group>) {
  return (
    <Group
      data-slot="resizable-panel-group"
      className={cn(
        'flex h-full w-full data-[panel-group-orientation=vertical]:flex-col',
        className,
      )}
      {...props}
    />
  );
}

function ResizablePanel({ className, ...props }: ComponentProps<typeof Panel>) {
  return (
    <Panel data-slot="resizable-panel" className={cn('min-h-0 min-w-0', className)} {...props} />
  );
}

function ResizableHandle({
  withHandle = true,
  className,
  ...props
}: ComponentProps<typeof Separator> & { withHandle?: boolean }) {
  return (
    <Separator
      data-slot="resizable-handle"
      className={cn(
        'relative flex w-px shrink-0 items-center justify-center bg-border',
        'after:absolute after:inset-y-0 after:left-1/2 after:w-4 after:-translate-x-1/2',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-1',
        'data-[orientation=vertical]:h-px data-[orientation=vertical]:w-full',
        'data-[orientation=vertical]:after:inset-x-0 data-[orientation=vertical]:after:h-4 data-[orientation=vertical]:after:w-auto',
        'data-[orientation=vertical]:after:left-0 data-[orientation=vertical]:after:translate-x-0 data-[orientation=vertical]:after:top-1/2 data-[orientation=vertical]:after:-translate-y-1/2',
        className,
      )}
      {...props}
    >
      {withHandle ? (
        <div className="z-10 flex h-4 w-3 items-center justify-center rounded-xs border border-border bg-surface">
          <GripVertical className="size-2.5 text-text-muted" />
        </div>
      ) : null}
    </Separator>
  );
}

export { ResizablePanelGroup, ResizablePanel, ResizableHandle };
