import { useMemo } from 'react';
import type { MouseEvent } from 'react';
import ReactMarkdown from 'react-markdown';
import type { Components, UrlTransform } from 'react-markdown';
import remarkGfm from 'remark-gfm';
import rehypeSanitize from 'rehype-sanitize';
import { defaultSchema } from 'hast-util-sanitize';

import { cn } from '@/lib/utils';

/**
 * TASK-MDR-01 (Module 24): the ONLY component allowed to render untrusted
 * Markdown (model output, user-submitted content, previews) anywhere in the
 * app - never use `dangerouslySetInnerHTML` or a bespoke Markdown pipeline
 * elsewhere (ASDD D19/§20.3, FR-OUT-002). Layered defenses:
 *  1. `react-markdown` never interprets embedded raw HTML in the source as
 *     real markup unless the `rehype-raw` plugin is added - it is
 *     deliberately NOT used here, so raw HTML (e.g. `<script>`) renders as
 *     inert escaped text, never executes.
 *  2. `rehype-sanitize` strips any remaining unsafe nodes/attributes
 *     (scripts, event handlers like `onerror`, iframes/embeds/objects,
 *     `style`) using a schema derived from `hast-util-sanitize`'s
 *     GitHub-style default, narrowed to only allow http/https/mailto link
 *     protocols (the upstream default also allows irc(s)/xmpp, which this
 *     app doesn't need).
 *  3. `urlTransform` independently re-validates every `href`/`src` at the
 *     react-markdown layer (belt-and-suspenders with step 2) using the same
 *     http/https/mailto allowlist; anything else (`javascript:`, `data:`,
 *     `vbscript:`, etc.) is dropped to an empty string.
 *
 * TASK-UX-10 (Module 37): citations are authored as standard GFM footnote
 * references (`text[^1]` ... `[^1]: ...`) - the default sanitize schema
 * already allows footnote markup (`data-footnote-ref`, `aria-describedby`,
 * etc.), so no schema changes were needed. When `onCitationClick` is passed,
 * footnote-reference links open the evidence drawer instead of jumping to
 * the in-page footnote definition; without it, SafeMarkdown behaves exactly
 * as before (plain external links / anchor jumps).
 */

const sanitizeSchema: typeof defaultSchema = {
  ...defaultSchema,
  protocols: {
    ...defaultSchema.protocols,
    href: ['http', 'https', 'mailto'],
  },
};

const SAFE_URL_PROTOCOL = /^(https?|mailto):/i;

const safeUrlTransform: UrlTransform = (url) => {
  const trimmed = url.trim();
  const hasProtocol = /^[a-z][a-z0-9+.-]*:/i.test(trimmed);
  // Relative/fragment URLs (no protocol, e.g. "#section", "./file") carry no
  // protocol risk and are left untouched.
  if (!hasProtocol) {
    return url;
  }
  return SAFE_URL_PROTOCOL.test(trimmed) ? url : '';
};

/** Extracts "3" out of remark-gfm's generated footnote href `#user-content-fn-3`. */
function footnoteIdFromHref(href: string | undefined): string | undefined {
  return href?.match(/fn-(.+)$/)?.[1];
}

/** react-markdown injects a `node` (hast) prop into every custom component; strip it before spreading the rest onto a real DOM element. */
function withoutNode<T extends { node?: unknown }>(props: T): Omit<T, 'node'> {
  const rest: Partial<T> = { ...props };
  delete rest.node;
  return rest as Omit<T, 'node'>;
}

function createMarkdownComponents(onCitationClick?: (footnoteId: string) => void): Components {
  return {
    a: ({ href, children, ...rawProps }) => {
      const props = withoutNode(rawProps);
      const dataAttributes = rawProps as unknown as Record<string, unknown>;
      const isFootnoteRef =
        dataAttributes['data-footnote-ref'] === true ||
        dataAttributes['data-footnote-ref'] === 'true';
      const footnoteId = isFootnoteRef ? footnoteIdFromHref(href) : undefined;

      if (footnoteId && onCitationClick) {
        const handleClick = (event: MouseEvent<HTMLAnchorElement>) => {
          event.preventDefault();
          onCitationClick(footnoteId);
        };
        return (
          <a
            {...props}
            href={href}
            onClick={handleClick}
            className="text-accent underline underline-offset-2 hover:no-underline"
          >
            {children}
          </a>
        );
      }

      return (
        <a
          {...props}
          href={href}
          target="_blank"
          rel="noopener noreferrer nofollow"
          className="text-accent underline underline-offset-2 hover:no-underline"
        >
          {children}
        </a>
      );
    },
    h1: ({ children, ...rawProps }) => (
      <h1
        {...withoutNode(rawProps)}
        className="mt-6 mb-3 text-2xl font-semibold text-text-primary first:mt-0"
      >
        {children}
      </h1>
    ),
    h2: ({ children, ...rawProps }) => (
      <h2
        {...withoutNode(rawProps)}
        className="mt-5 mb-2 text-xl font-semibold text-text-primary first:mt-0"
      >
        {children}
      </h2>
    ),
    h3: ({ children, ...rawProps }) => (
      <h3
        {...withoutNode(rawProps)}
        className="mt-4 mb-2 text-lg font-semibold text-text-primary first:mt-0"
      >
        {children}
      </h3>
    ),
    p: ({ children, ...rawProps }) => (
      <p {...withoutNode(rawProps)} className="mb-3 leading-relaxed text-text-primary last:mb-0">
        {children}
      </p>
    ),
    ul: ({ children, ...rawProps }) => (
      <ul {...withoutNode(rawProps)} className="mb-3 list-disc space-y-1 pl-6 text-text-primary">
        {children}
      </ul>
    ),
    ol: ({ children, ...rawProps }) => (
      <ol {...withoutNode(rawProps)} className="mb-3 list-decimal space-y-1 pl-6 text-text-primary">
        {children}
      </ol>
    ),
    blockquote: ({ children, ...rawProps }) => (
      <blockquote
        {...withoutNode(rawProps)}
        className="mb-3 border-l-2 border-border pl-4 text-text-secondary"
      >
        {children}
      </blockquote>
    ),
    code: ({ children, className, ...rawProps }) => (
      <code
        {...withoutNode(rawProps)}
        className={cn(
          'rounded bg-surface-hover px-1.5 py-0.5 font-mono text-[0.85em] text-text-primary',
          className,
        )}
      >
        {children}
      </code>
    ),
    pre: ({ children, ...rawProps }) => (
      <pre
        {...withoutNode(rawProps)}
        className="mb-3 overflow-x-auto rounded-md border border-border bg-surface-hover p-3 font-mono text-[0.85em] text-text-primary"
      >
        {children}
      </pre>
    ),
    table: ({ children, ...rawProps }) => (
      <div className="mb-3 overflow-x-auto">
        <table {...withoutNode(rawProps)} className="w-full border-collapse text-sm">
          {children}
        </table>
      </div>
    ),
    th: ({ children, ...rawProps }) => (
      <th
        {...withoutNode(rawProps)}
        className="border border-border bg-surface-hover px-2 py-1 text-left font-medium text-text-primary"
      >
        {children}
      </th>
    ),
    td: ({ children, ...rawProps }) => (
      <td {...withoutNode(rawProps)} className="border border-border px-2 py-1 text-text-primary">
        {children}
      </td>
    ),
    img: ({ alt, ...rawProps }) => (
      <img {...withoutNode(rawProps)} alt={alt ?? ''} className="max-w-full rounded-md" />
    ),
    section: ({ children, ...rawProps }) => {
      const props = withoutNode(rawProps);
      const dataAttributes = rawProps as unknown as Record<string, unknown>;
      const isFootnotesSection =
        dataAttributes['data-footnotes'] === true ||
        dataAttributes['data-footnotes'] === 'true' ||
        dataAttributes['data-footnotes'] === '' ||
        (typeof dataAttributes.className === 'string' &&
          dataAttributes.className.split(' ').includes('footnotes'));

      // remark-gfm appends a "Footnotes" section (heading + numbered list of
      // sources) after every message that has footnote-style citations. It's
      // meant to be screen-reader-only by default, but our custom `h2`
      // styling above clobbers that `sr-only` class, making it render as a
      // large, redundant heading under every cited answer. Citations are
      // already surfaced inline as clickable superscripts that open the
      // evidence drawer (see the `a` component above), so the trailing list
      // is pure noise here - drop the whole section.
      if (isFootnotesSection) {
        return null;
      }
      return <section {...props}>{children}</section>;
    },
  };
}

export interface SafeMarkdownProps {
  content: string;
  className?: string;
  /**
   * Called instead of the default anchor-jump when a GFM footnote-reference
   * citation marker (e.g. `[^1]`) is clicked - wire this to open the
   * evidence drawer (TASK-UX-10) with the matching footnote id.
   */
  onCitationClick?: (footnoteId: string) => void;
}

function SafeMarkdown({ content, className, onCitationClick }: SafeMarkdownProps) {
  const components = useMemo(() => createMarkdownComponents(onCitationClick), [onCitationClick]);

  return (
    <div className={cn('text-sm', className)}>
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        rehypePlugins={[[rehypeSanitize, sanitizeSchema]]}
        urlTransform={safeUrlTransform}
        components={components}
      >
        {content}
      </ReactMarkdown>
    </div>
  );
}

export { SafeMarkdown };
