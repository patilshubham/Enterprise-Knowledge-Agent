import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '@/api/problemDetails';
import { ErrorState } from '@/components/errors/ErrorState';

describe('ErrorState (TASK-ERR-02)', () => {
  it('renders the safe title, detail, and correlation ID from an ApiError', () => {
    render(
      <ErrorState
        error={
          new ApiError({
            title: 'Rate limited',
            status: 429,
            detail: 'Too many requests were made in a short period.',
            correlationId: 'trace-123',
            recommendedAction: 'Wait a moment and try again.',
            retryable: true,
          })
        }
        fallbackDetail="fallback"
      />,
    );

    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent('Rate limited');
    expect(alert).toHaveTextContent('Too many requests were made in a short period.');
    expect(alert).toHaveTextContent('Wait a moment and try again.');
    expect(alert).toHaveTextContent('trace-123');
  });

  it('falls back to the caller-supplied detail for a non-ApiError, never showing raw error text', () => {
    render(
      <ErrorState error={new Error('raw internal secret')} fallbackDetail="Failed to load data" />,
    );

    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent('Failed to load data');
    expect(alert).not.toHaveTextContent('raw internal secret');
  });

  it('shows a "Try again" button only when the error is retryable and onRetry is provided', () => {
    const onRetry = vi.fn();
    const { rerender } = render(
      <ErrorState
        error={new ApiError({ title: 'Timeout', status: 504, retryable: true })}
        fallbackDetail="fallback"
        onRetry={onRetry}
      />,
    );
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument();

    rerender(
      <ErrorState
        error={new ApiError({ title: 'Bad request', status: 400, retryable: false })}
        fallbackDetail="fallback"
        onRetry={onRetry}
      />,
    );
    expect(screen.queryByRole('button', { name: 'Try again' })).not.toBeInTheDocument();
  });
});
