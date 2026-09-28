/**
 * RFC 9457 Problem Details shape returned by the API's error-handling
 * middleware (see TASK-ERR-01). Kept minimal here; extended fields
 * (errorCategory, correlationId) are always present on API errors.
 */
export interface ProblemDetails {
  type?: string;
  title: string;
  status: number;
  detail?: string;
  instance?: string;
  correlationId?: string;
  errorCategory?: string;
  retryable?: boolean;
  recommendedAction?: string;
  [key: string]: unknown;
}

export class ApiError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails;

  constructor(problem: ProblemDetails) {
    super(problem.title);
    this.name = 'ApiError';
    this.status = problem.status;
    this.problem = problem;
  }
}

/**
 * TASK-ERR-02 (ASDD §22): the single, consistent way to turn any thrown
 * error into safe user-facing fields - never falls back to `error.message`
 * for non-`ApiError`s (that could be a raw JS/network error with internal
 * detail), only ever the caller-supplied `fallbackDetail`.
 */
export interface ErrorPresentation {
  title: string;
  detail: string;
  correlationId?: string;
  retryable: boolean;
  recommendedAction?: string;
}

export function describeApiError(error: unknown, fallbackDetail: string): ErrorPresentation {
  if (error instanceof ApiError) {
    return {
      title: error.problem.title,
      detail: error.problem.detail ?? fallbackDetail,
      correlationId: error.problem.correlationId,
      retryable: error.problem.retryable === true,
      recommendedAction:
        typeof error.problem.recommendedAction === 'string'
          ? error.problem.recommendedAction
          : undefined,
    };
  }

  return { title: 'Something went wrong', detail: fallbackDetail, retryable: false };
}
