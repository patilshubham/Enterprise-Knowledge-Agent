import { render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { StatusBadge } from '@/components/status/StatusBadge';
import { StageTimeline } from '@/components/timeline/StageTimeline';

// Mirrors the mock in test/lib/motion.test.tsx: motion-dom's
// prefers-reduced-motion state is a lazily-cached module singleton, so it
// must be mocked before the first render in this file and is not
// re-testable against the opposite value within the same file/process.
function mockMatchMedia(matches: boolean) {
  vi.stubGlobal(
    'matchMedia',
    vi.fn().mockImplementation((query: string) => ({
      matches,
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  );
}

describe('reduced motion (TASK-UX-09)', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('does not spin the StatusBadge processing icon when reduced motion is preferred', () => {
    mockMatchMedia(true);
    const { container } = render(<StatusBadge status="processing" />);
    expect(container.querySelector('svg.animate-spin')).not.toBeInTheDocument();
  });

  it('does not render the StageTimeline active-stage pulse when reduced motion is preferred', () => {
    mockMatchMedia(true);
    const { container } = render(
      <StageTimeline
        stages={[
          { id: 'uploaded', label: 'Uploaded', status: 'done' },
          { id: 'chunked', label: 'Chunked', status: 'active' },
          { id: 'ready', label: 'Ready', status: 'pending' },
        ]}
      />,
    );
    expect(container.querySelector('.bg-processing\\/40')).not.toBeInTheDocument();
    expect(screen.getByText('Chunked')).toBeInTheDocument();
  });
});
