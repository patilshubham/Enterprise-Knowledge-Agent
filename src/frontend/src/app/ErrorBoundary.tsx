import { Component, type ErrorInfo, type ReactNode } from 'react';
import { describeApiError } from '@/api/problemDetails';
import { Button } from '@/components/ui/button';

interface ErrorBoundaryProps {
  children: ReactNode;
}

interface ErrorBoundaryState {
  error: Error | null;
}

/**
 * TASK-ERR-02 (ASDD §22): global error boundary for uncaught render errors.
 * Surfaces the API's Problem Details title/safe detail/correlation ID via
 * the shared `describeApiError` parser (falling back to a generic message
 * for non-`ApiError`s), plus a "Reload" affordance - a full reload is the
 * only safe recovery here since the boundary itself doesn't know which
 * component tree state got the app into this state.
 */
export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = { error: null };

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo): void {
    console.error('Unhandled UI error', error, info.componentStack);
  }

  render() {
    if (this.state.error) {
      const presentation = describeApiError(
        this.state.error,
        'An unexpected error occurred. Please try again.',
      );
      return (
        <div role="alert">
          <h1>{presentation.title}</h1>
          <p>{presentation.detail}</p>
          {presentation.correlationId ? <p>Reference: {presentation.correlationId}</p> : null}
          <Button variant="outline" size="sm" onClick={() => window.location.reload()}>
            Reload
          </Button>
        </div>
      );
    }
    return this.props.children;
  }
}
