import { render, screen } from '@testing-library/react';
import { fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { DetailDrawer } from '@/components/timeline/DetailDrawer';

const stages = [
  { id: 'uploaded', label: 'Uploaded', status: 'done' as const },
  { id: 'chunked', label: 'Chunked', status: 'active' as const },
  { id: 'ready', label: 'Ready', status: 'pending' as const },
];

describe('DetailDrawer (TASK-UX-09)', () => {
  it('renders status, timeline, timings, initiator, and correlation ID', () => {
    render(
      <DetailDrawer
        open
        onOpenChange={vi.fn()}
        title="Ingestion run"
        status="processing"
        stages={stages}
        startedAt="10:42:03 AM"
        elapsed="2m 14s"
        initiator="jane.doe@example.com"
        correlationId="corr-abc-123"
      />,
    );

    expect(screen.getByText('Ingestion run')).toBeInTheDocument();
    expect(screen.getByText('Processing')).toBeInTheDocument();
    expect(screen.getByText('Uploaded')).toBeInTheDocument();
    expect(screen.getByText('Chunked')).toBeInTheDocument();
    expect(screen.getByText('Ready')).toBeInTheDocument();
    expect(screen.getByText('10:42:03 AM')).toBeInTheDocument();
    expect(screen.getByText('2m 14s')).toBeInTheDocument();
    expect(screen.getByText('jane.doe@example.com')).toBeInTheDocument();
    expect(screen.getByText('corr-abc-123')).toBeInTheDocument();
  });

  it('does not render a retry action when onRetry is not provided', () => {
    render(
      <DetailDrawer
        open
        onOpenChange={vi.fn()}
        title="Ingestion run"
        status="failed"
        stages={stages}
      />,
    );
    expect(screen.queryByRole('button', { name: 'Retry' })).not.toBeInTheDocument();
  });

  it('renders and invokes a retry action when onRetry is provided (caller-gated)', () => {
    const onRetry = vi.fn();
    render(
      <DetailDrawer
        open
        onOpenChange={vi.fn()}
        title="Ingestion run"
        status="failed"
        stages={stages}
        onRetry={onRetry}
      />,
    );

    const retryButton = screen.getByRole('button', { name: 'Retry' });
    fireEvent.click(retryButton);
    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  it('does not render the drawer content when closed', () => {
    render(
      <DetailDrawer
        open={false}
        onOpenChange={vi.fn()}
        title="Ingestion run"
        status="processing"
        stages={stages}
      />,
    );
    expect(screen.queryByText('Ingestion run')).not.toBeInTheDocument();
  });
});
