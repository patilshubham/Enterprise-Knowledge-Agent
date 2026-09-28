import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { ThemeContext } from '@/app/theme/theme-context';
import { OutputWorkspace } from '@/components/output/OutputWorkspace';
import type { EvidenceSource } from '@/components/evidence/EvidenceDrawer';

// Monaco can't mount in jsdom - see CodeEditor.test.tsx for the same mock.
vi.mock('monaco-editor', () => ({}));
vi.mock('@monaco-editor/react', () => ({
  loader: { config: vi.fn() },
  default: ({
    value,
    options,
  }: {
    value: string;
    options: { readOnly?: boolean; ariaLabel?: string };
  }) => (
    <textarea
      aria-label={options.ariaLabel}
      readOnly={options.readOnly}
      value={value}
      onChange={() => {}}
    />
  ),
}));

const content = 'The answer is 42[^1].\n\n[^1]: Because it is.';

const sources: EvidenceSource[] = [
  {
    id: '1',
    fileName: 'Onboarding Guide.md',
    headingPath: 'Onboarding Guide > Access Requests',
    passage: 'Because it is.',
  },
];

function renderWorkspace(overrides: Partial<React.ComponentProps<typeof OutputWorkspace>> = {}) {
  return render(
    <ThemeContext.Provider value={{ theme: 'dark', setTheme: vi.fn(), toggleTheme: vi.fn() }}>
      <OutputWorkspace content={content} sources={sources} {...overrides} />
    </ThemeContext.Provider>,
  );
}

describe('OutputWorkspace (TASK-UX-10)', () => {
  it('renders the Markdown output in Preview mode by default', () => {
    const { container } = renderWorkspace();
    expect(container.querySelector('a[data-footnote-ref]')).toBeInTheDocument();
  });

  it('citation click opens the EvidenceDrawer showing filename and heading path', () => {
    renderWorkspace();

    fireEvent.click(screen.getByRole('link', { name: '1' }));

    expect(screen.getByText('Evidence')).toBeInTheDocument();
    expect(screen.getByText('Onboarding Guide.md')).toBeInTheDocument();
    expect(screen.getByText('Onboarding Guide > Access Requests')).toBeInTheDocument();
  });

  it('does not navigate the page when a citation is clicked (sanitization preserved)', () => {
    const { container } = renderWorkspace();
    const link = container.querySelector('a[data-footnote-ref]');
    fireEvent.click(link!);
    // still on the same document, no href navigation occurred synchronously in jsdom
    expect(link).toHaveAttribute('href', '#user-content-fn-1');
  });

  it('renders staged execution progress instead of the output while executing', () => {
    renderWorkspace({ execution: { currentStage: 'generating' } });
    expect(screen.getByText('Generating')).toBeInTheDocument();
    expect(screen.getByText('Request accepted')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: '1' })).not.toBeInTheDocument();
  });

  it('renders a Markdown Source tab that shows the raw content', () => {
    const { container } = renderWorkspace();
    // Radix Tabs selects on mousedown, not click.
    fireEvent.mouseDown(screen.getByRole('tab', { name: 'Markdown Source' }));
    expect(container.querySelector('textarea')).toHaveValue(content);
  });

  it('renders an optional runDetailsAction slot', () => {
    renderWorkspace({ runDetailsAction: <button type="button">Run details</button> });
    expect(screen.getByRole('button', { name: 'Run details' })).toBeInTheDocument();
  });
});
