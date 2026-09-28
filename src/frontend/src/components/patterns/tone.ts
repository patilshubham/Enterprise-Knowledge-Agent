import type { CSSProperties } from 'react';

/**
 * Redesign: the small, shared palette of semantic "tones" used across the new
 * premium primitives (metric tiles, section cards, pipeline stages, empty
 * states). Every tone maps onto an existing semantic CSS token - never a new
 * hard-coded color - so light/dark theming stays correct.
 */
export type Tone = 'neutral' | 'accent' | 'success' | 'warning' | 'danger' | 'processing';

const TONE_VAR: Record<Tone, string> = {
  neutral: 'var(--text-muted)',
  accent: 'var(--accent)',
  success: 'var(--success)',
  warning: 'var(--warning)',
  danger: 'var(--danger)',
  processing: 'var(--processing)',
};

/** Inline style that drives the `.icon-chip` (and any `--chip-color` consumer) for a tone. */
export function chipStyle(tone: Tone): CSSProperties {
  return { ['--chip-color' as string]: TONE_VAR[tone] };
}
