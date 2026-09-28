export type ReadinessState = 'healthy' | 'degraded' | 'unhealthy' | 'unknown';

/**
 * TASK-UX-04: lightweight readiness probe for the header's health
 * indicator. `/health/ready` is ASP.NET Core's default health-check
 * endpoint - it returns plain text (`Healthy`/`Degraded`/`Unhealthy`), not
 * JSON, so this deliberately bypasses `apiClient`'s Problem-Details parsing.
 * Any network failure (offline, proxy down) degrades to `'unknown'` rather
 * than throwing - the indicator itself decides how to render that.
 */
export async function fetchReadiness(): Promise<ReadinessState> {
  try {
    const response = await fetch('/health/ready', { credentials: 'include' });
    const body = (await response.text()).trim().toLowerCase();

    if (body === 'healthy' || body === 'degraded' || body === 'unhealthy') {
      return body;
    }
    return response.ok ? 'healthy' : 'unhealthy';
  } catch {
    return 'unknown';
  }
}
