import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { ThemeContext } from '@/app/theme/theme-context';
import { CodeEditor } from '@/components/editor/CodeEditor';

// Monaco itself requires a real browser (canvas, workers, layout) and
// cannot mount - or even fully import - in jsdom (its ESM bundle touches
// browser-only APIs like `document.queryCommandSupported` at import time).
// Mock both the bare `monaco-editor` package (imported by our wrapper only
// to configure the local, non-CDN loader) and `@monaco-editor/react`'s
// `Editor` with a plain textarea that mirrors the same `value`/`onChange`/
// `options` contract, so this test verifies our wrapper's wiring (props
// passed through, onChange receives the full untruncated text) rather than
// Monaco's own internals.
vi.mock('monaco-editor', () => ({}));
vi.mock('@monaco-editor/react', () => ({
  loader: { config: vi.fn() },
  default: ({
    value,
    onChange,
    language,
    theme,
    options,
  }: {
    value: string;
    onChange: (value: string | undefined) => void;
    language: string;
    theme: string;
    options: { readOnly?: boolean; ariaLabel?: string };
  }) => (
    <textarea
      aria-label={options.ariaLabel}
      data-language={language}
      data-theme={theme}
      readOnly={options.readOnly}
      value={value}
      onChange={(event) => onChange(event.target.value)}
    />
  ),
}));

function renderEditor(overrides: Partial<React.ComponentProps<typeof CodeEditor>> = {}) {
  return render(
    <ThemeContext.Provider value={{ theme: 'dark', setTheme: vi.fn(), toggleTheme: vi.fn() }}>
      <CodeEditor value="# Hello" onChange={vi.fn()} ariaLabel="Prompt editor" {...overrides} />
    </ThemeContext.Provider>,
  );
}

describe('CodeEditor (TASK-UX-07)', () => {
  it('mounts with the given value, language and dark theme', () => {
    renderEditor({ language: 'markdown' });

    const editor = screen.getByLabelText('Prompt editor');
    expect(editor).toHaveValue('# Hello');
    expect(editor).toHaveAttribute('data-language', 'markdown');
    expect(editor).toHaveAttribute('data-theme', 'vs-dark');
  });

  it('reports edits back via onChange without truncating content', () => {
    const onChange = vi.fn();
    renderEditor({ onChange });

    const longValue = `# Title\n\n${'x'.repeat(5000)}`;
    const editor = screen.getByLabelText('Prompt editor');
    (editor as HTMLTextAreaElement).focus();
    Object.defineProperty(editor, 'value', { value: longValue, writable: true });
    editor.dispatchEvent(new Event('change', { bubbles: true }));

    expect(onChange).toHaveBeenCalledWith(longValue);
    expect(onChange.mock.calls[0][0]).toHaveLength(longValue.length);
  });

  it('marks the editor read-only when requested', () => {
    renderEditor({ readOnly: true });

    expect(screen.getByLabelText('Prompt editor')).toHaveAttribute('readonly');
  });
});
