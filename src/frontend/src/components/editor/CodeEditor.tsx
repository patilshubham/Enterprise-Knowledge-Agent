import { useCallback, useRef } from 'react';
import Editor, { loader, type OnMount } from '@monaco-editor/react';
import * as monaco from 'monaco-editor';

import { useTheme } from '@/app/theme/theme-context';
import { cn } from '@/lib/utils';

// TASK-UX-07 (Module 37): point the loader at the npm `monaco-editor` package
// instead of letting `@monaco-editor/react` fetch a copy from a public CDN at
// runtime (its default behavior). This keeps the editor fully self-hosted -
// no third-party script origin, no version drift, and nothing phoning home.
loader.config({ monaco });

export interface CodeEditorProps {
  /** Current editor contents (controlled). */
  value: string;
  /** Called on every keystroke with the full, untruncated editor contents. */
  onChange: (value: string) => void;
  /** Monaco language id; defaults to `markdown` (the primary large-text use case). */
  language?: string;
  readOnly?: boolean;
  /** CSS height of the editor; defaults to filling its container. */
  height?: string | number;
  /** Accessible name for the hidden textarea Monaco renders for screen readers. */
  ariaLabel: string;
  className?: string;
}

/**
 * Shared Monaco wrapper for large prompt/Markdown editing (agent prompt
 * editor, Markdown source view). Line numbers, the built-in find widget
 * (Ctrl/Cmd+F), and standard keyboard navigation are all on by default;
 * `automaticLayout` keeps it correctly sized inside a `ResizablePanel`.
 */
function CodeEditor({
  value,
  onChange,
  language = 'markdown',
  readOnly = false,
  height = '100%',
  ariaLabel,
  className,
}: CodeEditorProps) {
  const { theme } = useTheme();
  const editorRef = useRef<monaco.editor.IStandaloneCodeEditor | null>(null);

  const handleMount: OnMount = useCallback((editorInstance) => {
    editorRef.current = editorInstance;
  }, []);

  const handleChange = useCallback(
    (nextValue: string | undefined) => {
      onChange(nextValue ?? '');
    },
    [onChange],
  );

  return (
    <div data-slot="code-editor" className={cn('h-full min-h-0 w-full', className)}>
      <Editor
        height={height}
        language={language}
        value={value}
        onChange={handleChange}
        onMount={handleMount}
        theme={theme === 'dark' ? 'vs-dark' : 'light'}
        options={{
          readOnly,
          ariaLabel,
          automaticLayout: true,
          minimap: { enabled: false },
          wordWrap: 'on',
          fontFamily: 'var(--font-mono)',
          fontSize: 13,
          lineNumbers: 'on',
          tabSize: 2,
          scrollBeyondLastLine: false,
          renderWhitespace: 'none',
          padding: { top: 12, bottom: 12 },
        }}
        loading={null}
      />
    </div>
  );
}

export { CodeEditor };
