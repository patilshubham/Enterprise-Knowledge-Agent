import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '@/api/problemDetails';
import { ErrorBoundary } from '@/app/ErrorBoundary';

function Bomb({ error }: { error: Error }): never {
  throw error;
}

describe('ErrorBoundary (TASK-ERR-02)', () => {
  it('renders children when there is no error', () => {
    render(
      <ErrorBoundary>
        <p>content</p>
      </ErrorBoundary>,
    );

    expect(screen.getByText('content')).toBeInTheDocument();
  });

  it('surfaces the safe title/detail/correlation ID for an uncaught ApiError', () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});

    render(
      <ErrorBoundary>
        <Bomb
          error={
            new ApiError({
              title: 'Dependency unavailable',
              status: 503,
              detail: 'A required dependency is temporarily unavailable.',
              correlationId: 'trace-xyz',
            })
          }
        />
      </ErrorBoundary>,
    );

    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent('Dependency unavailable');
    expect(alert).toHaveTextContent('A required dependency is temporarily unavailable.');
    expect(alert).toHaveTextContent('trace-xyz');
    expect(screen.getByRole('button', { name: 'Reload' })).toBeInTheDocument();

    vi.restoreAllMocks();
  });

  it('shows a generic safe message for a non-ApiError, never the raw error text', () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});

    render(
      <ErrorBoundary>
        <Bomb error={new Error('raw internal stack detail')} />
      </ErrorBoundary>,
    );

    const alert = screen.getByRole('alert');
    expect(alert).not.toHaveTextContent('raw internal stack detail');

    vi.restoreAllMocks();
  });
});
