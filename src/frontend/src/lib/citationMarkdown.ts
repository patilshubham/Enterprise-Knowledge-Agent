import type { CitationDetailResponse } from '@/api/outputs';
import type { EvidenceSource } from '@/components/evidence/EvidenceDrawer';

/** Matches module 22's guaranteed `[SOURCE-NNN]` marker format (see `CitationMarkerValidator`/`PromptAssembler`). */
const SOURCE_MARKER_PATTERN = /\[SOURCE-\d{3}\]/g;

export interface CitationMarkdownResult {
  /** `markdown` with every recognized `[SOURCE-NNN]` marker rewritten to a GFM footnote reference, plus appended footnote definitions. */
  markdown: string;
  /** One entry per distinct citation actually referenced, in first-appearance order - feed directly to `OutputWorkspace`/`EvidenceDrawer`. */
  sources: EvidenceSource[];
}

/**
 * TASK-OUT-02/CIT-04: bridges the raw persisted Markdown (which contains
 * literal `[SOURCE-NNN]` citation markers - see `GeneratedOutput.OriginalMarkdown`/
 * `CurrentMarkdown`) into the GFM footnote syntax (`[^source-nnn]` +
 * `[^source-nnn]: ...`) that `SafeMarkdown`/`OutputWorkspace` (TASK-UX-10)
 * already know how to render as clickable citations wired to the evidence
 * drawer. Markers with no matching citation (shouldn't happen post
 * TASK-CIT-03 validation, but defensively handled) are left untouched as
 * plain text.
 */
export function buildCitationMarkdown(
  markdown: string,
  citations: readonly CitationDetailResponse[],
): CitationMarkdownResult {
  const bySourceId = new Map(citations.map((citation) => [citation.sourceId, citation]));
  const citationByFootnoteId = new Map<string, CitationDetailResponse>();
  const footnoteIds: string[] = [];

  const rewritten = markdown.replace(SOURCE_MARKER_PATTERN, (match) => {
    const sourceId = match.slice(1, -1);
    const citation = bySourceId.get(sourceId);
    if (!citation) {
      return match;
    }

    const footnoteId = sourceId.toLowerCase();
    if (!citationByFootnoteId.has(footnoteId)) {
      citationByFootnoteId.set(footnoteId, citation);
      footnoteIds.push(footnoteId);
    }
    return `[^${footnoteId}]`;
  });

  if (footnoteIds.length === 0) {
    return { markdown, sources: [] };
  }

  const definitions = footnoteIds.map((footnoteId) => `[^${footnoteId}]: ${footnoteId}`).join('\n');

  const sources: EvidenceSource[] = footnoteIds.map((footnoteId) => {
    const citation = citationByFootnoteId.get(footnoteId)!;
    return {
      id: footnoteId,
      fileName: citation.fileName,
      headingPath: citation.headingPath || undefined,
      passage: citation.passage,
      deleted: citation.sourceUnavailable,
    };
  });

  return { markdown: `${rewritten}\n\n${definitions}\n`, sources };
}
