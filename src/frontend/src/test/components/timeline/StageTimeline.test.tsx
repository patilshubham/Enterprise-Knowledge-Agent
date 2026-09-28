import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { StageTimeline } from '@/components/timeline/StageTimeline';

describe('StageTimeline (TASK-UX-09)', () => {
  const stages = [
    { id: 'uploaded', label: 'Uploaded', status: 'done' as const },
    { id: 'validated', label: 'Validated', status: 'done' as const },
    { id: 'chunked', label: 'Chunked', status: 'active' as const, detail: 'Embedding 42/128' },
    { id: 'indexed', label: 'Indexed', status: 'pending' as const },
    { id: 'ready', label: 'Ready', status: 'pending' as const },
  ];

  it('renders every stage label', () => {
    render(<StageTimeline stages={stages} />);
    for (const stage of stages) {
      expect(screen.getByText(stage.label)).toBeInTheDocument();
    }
  });

  it('renders per-stage progress detail text', () => {
    render(<StageTimeline stages={stages} />);
    expect(screen.getByText('Embedding 42/128')).toBeInTheDocument();
  });

  it('conveys each stage state with a screen-reader status word, not color alone', () => {
    render(<StageTimeline stages={stages} />);
    expect(screen.getAllByText(/Completed/)).toHaveLength(2);
    expect(screen.getByText(/In progress/)).toBeInTheDocument();
    expect(screen.getAllByText(/Pending/)).toHaveLength(2);
  });

  it('renders a failed stage with its own distinct marker and status word', () => {
    render(
      <StageTimeline
        stages={[
          { id: 'uploaded', label: 'Uploaded', status: 'done' },
          { id: 'validated', label: 'Validated', status: 'failed' },
        ]}
      />,
    );
    expect(screen.getByText(/Failed/)).toBeInTheDocument();
  });

  it('renders the active-stage pulse by default (motion not reduced)', () => {
    const { container } = render(<StageTimeline stages={stages} />);
    expect(container.querySelector('.bg-processing\\/40')).toBeInTheDocument();
  });
});
