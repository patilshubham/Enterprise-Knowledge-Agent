import { act, render, screen } from '@testing-library/react';
import { useRef } from 'react';
import { describe, expect, it } from 'vitest';
import type { GroupImperativeHandle } from 'react-resizable-panels';

import { ResizableHandle, ResizablePanel, ResizablePanelGroup } from '@/components/ui/resizable';

function TestSplit({ onGroupRef }: { onGroupRef: (ref: GroupImperativeHandle | null) => void }) {
  const groupRef = useRef<GroupImperativeHandle | null>(null);

  return (
    <ResizablePanelGroup
      orientation="horizontal"
      style={{ height: 200 }}
      groupRef={(instance) => {
        groupRef.current = instance;
        onGroupRef(instance);
      }}
    >
      <ResizablePanel id="left" defaultSize={50} minSize={20}>
        Left content
      </ResizablePanel>
      <ResizableHandle />
      <ResizablePanel id="right" defaultSize={50} minSize={20}>
        Right content
      </ResizablePanel>
    </ResizablePanelGroup>
  );
}

describe('Resizable panel primitives (TASK-UX-07)', () => {
  it('renders both panels and an accessible separator', () => {
    render(<TestSplit onGroupRef={() => {}} />);

    expect(screen.getByText('Left content')).toBeInTheDocument();
    expect(screen.getByText('Right content')).toBeInTheDocument();
    expect(screen.getByRole('separator')).toBeInTheDocument();
  });

  it('exposes an accessible, keyboard-operable separator between panels', () => {
    render(<TestSplit onGroupRef={() => {}} />);

    const separator = screen.getByRole('separator');
    // react-resizable-panels requires separators to be focusable and to
    // advertise horizontal/vertical orientation so keyboard users (arrow
    // keys, Home/End, Enter) can resize panels without a mouse. Exact pixel
    // resize math depends on real layout measurement (`getBoundingClientRect`),
    // which jsdom always reports as zero, so it isn't reproducible here -
    // this asserts the accessibility contract the drag/keyboard interaction
    // relies on instead.
    expect(separator).toHaveAttribute('tabIndex', '0');
    expect(separator).toHaveAttribute('aria-orientation', 'vertical');
  });

  it('exposes the imperative group layout API without throwing', () => {
    let groupHandle: GroupImperativeHandle | null = null;
    render(
      <TestSplit
        onGroupRef={(instance) => {
          groupHandle = instance;
        }}
      />,
    );

    expect(groupHandle).not.toBeNull();
    expect(() => {
      act(() => {
        groupHandle!.setLayout({ left: 30, right: 70 });
      });
    }).not.toThrow();
    expect(typeof groupHandle!.getLayout()).toBe('object');
  });
});
