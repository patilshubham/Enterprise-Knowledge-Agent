import { describeApiError } from '@/api/problemDetails';
import { Button } from '@/components/ui/button';

interface ErrorStateProps {
  error: unknown;
  fallbackDetail: string;
  onRetry?: () => void;
}

/**
 * TASK-ERR-02 (ASDD §22): the single reusable way to render a failed
 * query/action - safe title + detail (never raw exception text), a
 * correlation ID for support, and a "Try again" button when the server
 * indicated the failure is retryable AND the caller wired up `onRetry`.
 */
export function ErrorState({ error, fallbackDetail, onRetry }: ErrorStateProps) {
  const presentation = describeApiError(error, fallbackDetail);

  return (
    <div role="alert" className="space-y-2 text-sm text-danger">
      <p className="font-medium">{presentation.title}</p>
      <p>{presentation.detail}</p>
      {presentation.recommendedAction && (
        <p className="text-text-muted">{presentation.recommendedAction}</p>
      )}
      {presentation.correlationId && (
        <p className="text-text-muted">Reference: {presentation.correlationId}</p>
      )}
      {presentation.retryable && onRetry && (
        <Button variant="outline" size="sm" onClick={onRetry}>
          Try again
        </Button>
      )}
    </div>
  );
}
