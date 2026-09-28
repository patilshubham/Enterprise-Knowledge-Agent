import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { StatusBadge } from '@/components/status/StatusBadge';

// Note: reduced-motion behavior is covered separately in
// StatusBadge.reduced-motion.test.tsx - motion-dom's prefers-reduced-motion
// state is a module-level singleton that's lazily cached on first read, so
// only one matchMedia scenario can be meaningfully asserted per test file
// (see motion.test.tsx for the same documented limitation). This file
// relies on jsdom's default `window.matchMedia` being undefined, which the
// hook treats as "not reduced".
describe('StatusBadge (TASK-UX-09)', () => {
  it('renders an icon alongside the text label for every status kind', () => {
    const statuses = ['queued', 'processing', 'ready', 'warning', 'failed', 'disabled'] as const;

    for (const status of statuses) {
      const { container, unmount } = render(<StatusBadge status={status} />);
      expect(container.querySelector('svg')).toBeInTheDocument();
      expect(container.textContent).not.toBe('');
      unmount();
    }
  });

  it('shows a custom label when provided', () => {
    render(<StatusBadge status="ready" label="All embeddings indexed" />);
    expect(screen.getByText('All embeddings indexed')).toBeInTheDocument();
  });

  it('spins the processing icon by default', () => {
    const { container } = render(<StatusBadge status="processing" />);
    expect(container.querySelector('svg.animate-spin')).toBeInTheDocument();
  });
});
