/**
 * TASK-UX-08 (Module 37): shared chart color tokens - every trend chart
 * reads these instead of hardcoding hex values, so charts stay in sync with
 * the light/dark design tokens defined in `index.css` (`@theme inline`).
 */
export type ChartColorToken = 'accent' | 'success' | 'warning' | 'danger' | 'processing';

export const CHART_COLOR_VARS: Record<ChartColorToken, string> = {
  accent: 'var(--color-accent)',
  success: 'var(--color-success)',
  warning: 'var(--color-warning)',
  danger: 'var(--color-danger)',
  processing: 'var(--color-processing)',
};

/** Default series color order when a series doesn't specify one explicitly. */
export const CHART_COLOR_ORDER: ChartColorToken[] = [
  'accent',
  'processing',
  'success',
  'warning',
  'danger',
];

export const CHART_GRID_COLOR = 'var(--color-border)';
export const CHART_AXIS_COLOR = 'var(--color-text-muted)';
export const CHART_TOOLTIP_BACKGROUND = 'var(--color-surface-raised)';
export const CHART_TOOLTIP_BORDER = 'var(--color-border)';
export const CHART_TOOLTIP_TEXT = 'var(--color-text-primary)';
