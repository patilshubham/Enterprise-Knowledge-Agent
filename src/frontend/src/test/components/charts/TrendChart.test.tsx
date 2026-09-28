import { render, screen } from '@testing-library/react';
import { afterAll, beforeAll, describe, expect, it, vi } from 'vitest';

import { TrendChart } from '@/components/charts/TrendChart';

// Recharts' `ResponsiveContainer` measures its host element via
// `getBoundingClientRect()` (and a ResizeObserver for later updates) to size
// the chart - jsdom always reports zero-size layout, so without this the
// chart silently renders nothing (no <svg>, no error). Stub a real-looking
// rect for the duration of this file only.
beforeAll(() => {
  vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockReturnValue({
    width: 500,
    height: 300,
    top: 0,
    left: 0,
    bottom: 300,
    right: 500,
    x: 0,
    y: 0,
    toJSON: () => {},
  });
});

afterAll(() => {
  vi.restoreAllMocks();
});

interface DataPoint {
  day: string;
  completed: number;
  failed: number;
}

const data: DataPoint[] = [
  { day: 'Mon', completed: 12, failed: 1 },
  { day: 'Tue', completed: 18, failed: 2 },
  { day: 'Wed', completed: 9, failed: 0 },
];

describe('TrendChart (TASK-UX-08)', () => {
  it('renders a titled card with an svg chart when data is present', () => {
    const { container } = render(
      <TrendChart<DataPoint>
        variant="line"
        data={data}
        xKey="day"
        series={[
          { key: 'completed', label: 'Completed' },
          { key: 'failed', label: 'Failed', color: 'danger' },
        ]}
        title="Processing volume"
        description="Completed vs failed jobs per day"
      />,
    );

    expect(screen.getByText('Processing volume')).toBeInTheDocument();
    expect(screen.getByText('Completed vs failed jobs per day')).toBeInTheDocument();
    expect(container.querySelector('svg.recharts-surface')).toBeInTheDocument();
    expect(screen.queryByText('No data yet.')).not.toBeInTheDocument();
  });

  it('renders each chart variant without crashing', () => {
    for (const variant of ['line', 'area', 'bar'] as const) {
      const { container, unmount } = render(
        <TrendChart<DataPoint>
          variant={variant}
          data={data}
          xKey="day"
          series={[{ key: 'completed', label: 'Completed' }]}
        />,
      );
      expect(container.querySelector('svg.recharts-surface')).toBeInTheDocument();
      unmount();
    }
  });

  it('shows the default empty state when there is no data', () => {
    render(
      <TrendChart<DataPoint>
        variant="line"
        data={[]}
        xKey="day"
        series={[{ key: 'completed', label: 'Completed' }]}
      />,
    );

    expect(screen.getByText('No data yet.')).toBeInTheDocument();
  });

  it('shows a custom empty state message when there is no data', () => {
    render(
      <TrendChart<DataPoint>
        variant="bar"
        data={[]}
        xKey="day"
        series={[{ key: 'completed', label: 'Completed' }]}
        emptyState="No processing activity in this range."
      />,
    );

    expect(screen.getByText('No processing activity in this range.')).toBeInTheDocument();
  });
});
