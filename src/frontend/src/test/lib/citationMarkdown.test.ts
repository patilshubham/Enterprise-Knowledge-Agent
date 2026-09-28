import { describe, expect, it } from 'vitest';
import type { CitationDetailResponse } from '@/api/outputs';
import { buildCitationMarkdown } from '@/lib/citationMarkdown';

function makeCitation(overrides: Partial<CitationDetailResponse> = {}): CitationDetailResponse {
  return {
    id: 'citation-1',
    sourceId: 'SOURCE-001',
    fileName: 'policy.md',
    headingPath: 'Onboarding > Access',
    passage: 'Requests are approved within two business days.',
    characterStart: 10,
    characterEnd: 60,
    processedAt: '2024-01-01T00:00:00Z',
    sourceUnavailable: false,
    ...overrides,
  };
}

describe('buildCitationMarkdown', () => {
  it('rewrites [SOURCE-NNN] markers into GFM footnote references with definitions', () => {
    const result = buildCitationMarkdown('Approved quickly [SOURCE-001].', [makeCitation()]);

    expect(result.markdown).toContain('Approved quickly [^source-001].');
    expect(result.markdown).toContain('[^source-001]: source-001');
    expect(result.sources).toEqual([
      {
        id: 'source-001',
        fileName: 'policy.md',
        headingPath: 'Onboarding > Access',
        passage: 'Requests are approved within two business days.',
        deleted: false,
      },
    ]);
  });

  it('deduplicates repeated markers into a single footnote/source entry', () => {
    const result = buildCitationMarkdown('First claim [SOURCE-001]. Second claim [SOURCE-001].', [
      makeCitation(),
    ]);

    expect(result.sources).toHaveLength(1);
    expect(result.markdown.match(/\[\^source-001\](?!:)/g)).toHaveLength(2);
    expect(result.markdown.match(/\[\^source-001\]: source-001/g)).toHaveLength(1);
  });

  it('preserves appearance order across multiple distinct citations', () => {
    const result = buildCitationMarkdown('Claim A [SOURCE-002]. Claim B [SOURCE-001].', [
      makeCitation({ id: 'c1', sourceId: 'SOURCE-001' }),
      makeCitation({ id: 'c2', sourceId: 'SOURCE-002' }),
    ]);

    expect(result.sources.map((s) => s.id)).toEqual(['source-002', 'source-001']);
  });

  it('maps sourceUnavailable to the EvidenceSource "deleted" flag', () => {
    const result = buildCitationMarkdown('Claim [SOURCE-001].', [
      makeCitation({ sourceUnavailable: true }),
    ]);

    expect(result.sources[0].deleted).toBe(true);
  });

  it('leaves unmatched markers untouched (no citation found for that SOURCE id)', () => {
    const result = buildCitationMarkdown('Claim [SOURCE-999].', [makeCitation()]);

    expect(result.markdown).toBe('Claim [SOURCE-999].');
    expect(result.sources).toEqual([]);
  });

  it('returns the original markdown unchanged when there are no citations', () => {
    const result = buildCitationMarkdown('No citations here.', []);

    expect(result.markdown).toBe('No citations here.');
    expect(result.sources).toEqual([]);
  });
});
