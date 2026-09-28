import { render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { ThemeContext } from '@/app/theme/theme-context';
import { OutputWorkspace } from '@/components/output/OutputWorkspace';
import type { EvidenceSource } from '@/components/evidence/EvidenceDrawer';

vi.mock('monaco-editor', () => ({}));
vi.mock('@monaco-editor/react', () => ({
  loader: { config: vi.fn() },
  default: () => <textarea aria-label="Markdown source" readOnly />,
}));

// Mirrors src/test/components/status/reduced-motion.test.tsx: the
// prefers-reduced-motion state is a lazily-cached module singleton, so it
// must be mocked before the first render in this file (only one scenario
// is meaningfully testable per file/process).
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

const sources: EvidenceSource[] = [];

describe('OutputWorkspace reduced motion (TASK-UX-10)', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('shows the output immediately, with no opacity fade-in, when reduced motion is preferred', () => {
    mockMatchMedia(true);
    const { container } = render(
      <ThemeContext.Provider value={{ theme: 'dark', setTheme: vi.fn(), toggleTheme: vi.fn() }}>
        <OutputWorkspace content="Hello world" sources={sources} />
      </ThemeContext.Provider>,
    );

    expect(screen.getByText('Hello world')).toBeInTheDocument();
    // With reduced motion, `initial={false}` skips the opacity:0 starting
    // state entirely - Motion renders straight at its animate target (1),
    // never at 0.
    expect(container.querySelector('[style*="opacity: 0"]')).not.toBeInTheDocument();
  });
});
