import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { SafeMarkdown } from '@/components/markdown/SafeMarkdown';

describe('SafeMarkdown (TASK-MDR-01)', () => {
  it('never leaks react-markdown internal props (e.g. "node") onto rendered DOM elements', () => {
    const { container } = render(<SafeMarkdown content={'# Heading\n\nA paragraph.'} />);
    expect(container.querySelector('[node]')).not.toBeInTheDocument();
  });

  describe('malicious Markdown payloads are neutralized', () => {
    it('never renders a real <script> element from an embedded <script> tag', () => {
      const { container } = render(
        <SafeMarkdown content={'Before\n\n<script>window.__pwned = true;</script>\n\nAfter'} />,
      );
      expect(container.querySelector('script')).not.toBeInTheDocument();
      expect((window as unknown as { __pwned?: boolean }).__pwned).toBeUndefined();
    });

    it('strips a javascript: URL from a link href', () => {
      const { container } = render(<SafeMarkdown content={'[Click me](javascript:alert(1))'} />);
      const link = container.querySelector('a');
      expect(link).toBeInTheDocument();
      expect(link?.getAttribute('href') ?? '').not.toContain('javascript:');
    });

    it('strips a data: URL from a link href', () => {
      const { container } = render(
        <SafeMarkdown content={'[Click me](data:text/html,<script>alert(1)</script>)'} />,
      );
      const link = container.querySelector('a');
      expect(link?.getAttribute('href') ?? '').not.toMatch(/^data:/);
    });

    it('strips inline event-handler attributes from raw HTML (e.g. onerror)', () => {
      const { container } = render(
        <SafeMarkdown content={'<img src="x" onerror="window.__pwned = true">'} />,
      );
      expect(container.querySelector('img[onerror]')).not.toBeInTheDocument();
      expect((window as unknown as { __pwned?: boolean }).__pwned).toBeUndefined();
    });

    it('does not render iframes or embeds', () => {
      const { container } = render(
        <SafeMarkdown content={'<iframe src="https://example.com"></iframe>'} />,
      );
      expect(container.querySelector('iframe')).not.toBeInTheDocument();
    });

    it('only allows http/https/mailto protocols for a safe reference link', () => {
      const { container } = render(
        <SafeMarkdown content={'[Contact us](mailto:team@example.com)'} />,
      );
      const link = container.querySelector('a');
      expect(link?.getAttribute('href')).toBe('mailto:team@example.com');
    });
  });

  describe('safe content is preserved', () => {
    it('renders headings, paragraphs, and an https link', () => {
      render(
        <SafeMarkdown
          content={'# Heading\n\nSome paragraph text.\n\n[Docs](https://example.com/docs)'}
        />,
      );
      expect(screen.getByRole('heading', { level: 1, name: 'Heading' })).toBeInTheDocument();
      expect(screen.getByText('Some paragraph text.')).toBeInTheDocument();
      const link = screen.getByRole('link', { name: 'Docs' });
      expect(link).toHaveAttribute('href', 'https://example.com/docs');
    });

    it('renders GFM tables', () => {
      render(<SafeMarkdown content={'| A | B |\n| --- | --- |\n| 1 | 2 |'} />);
      expect(screen.getByRole('table')).toBeInTheDocument();
      expect(screen.getByText('A')).toBeInTheDocument();
      expect(screen.getByText('2')).toBeInTheDocument();
    });

    it('renders fenced code blocks', () => {
      const { container } = render(<SafeMarkdown content={'```js\nconst x = 1;\n```'} />);
      expect(container.querySelector('pre code')).toBeInTheDocument();
      expect(screen.getByText('const x = 1;')).toBeInTheDocument();
    });

    it('renders GFM strikethrough', () => {
      const { container } = render(<SafeMarkdown content={'~~deleted~~'} />);
      expect(container.querySelector('del')).toBeInTheDocument();
    });
  });

  describe('citation interaction (TASK-UX-10)', () => {
    const content = 'The answer is 42[^1].\n\n[^1]: Because it is.';

    it('invokes onCitationClick with the footnote id instead of navigating, when provided', () => {
      const onCitationClick = vi.fn();
      const { container } = render(
        <SafeMarkdown content={content} onCitationClick={onCitationClick} />,
      );
      const citationLink = container.querySelector('a[data-footnote-ref]');
      expect(citationLink).toBeInTheDocument();

      fireEvent.click(citationLink!);
      expect(onCitationClick).toHaveBeenCalledWith('1');
    });

    it('falls back to a plain anchor-jump link when onCitationClick is not provided', () => {
      const { container } = render(<SafeMarkdown content={content} />);
      const citationLink = container.querySelector('a[data-footnote-ref]');
      expect(citationLink).toHaveAttribute('href', '#user-content-fn-1');
    });

    it('does not render the generated Footnotes section (redundant with the evidence drawer)', () => {
      const { container } = render(<SafeMarkdown content={content} />);
      expect(screen.queryByRole('heading', { name: 'Footnotes' })).not.toBeInTheDocument();
      expect(container.querySelector('section[data-footnotes]')).not.toBeInTheDocument();
      // The inline citation reference itself must still render.
      expect(container.querySelector('a[data-footnote-ref]')).toBeInTheDocument();
    });
  });
});
