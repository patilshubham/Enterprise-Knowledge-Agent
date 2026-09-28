import { ApiError, type ProblemDetails } from './problemDetails';
import { API_BASE_URL } from './config';

/**
 * TASK-OBS-01 (ASDD §23.1): every request starts the browser→API→job→
 * queue→worker→AOAI→output→SignalR correlation chain here - the backend's
 * `CorrelationIdMiddleware` reuses this value verbatim if present instead of
 * minting its own.
 */
const CORRELATION_HEADER_NAME = 'X-Correlation-Id';

export type ApiRequestInit = Omit<RequestInit, 'body'> & {
  body?: unknown;
};

/**
 * Typed fetch wrapper used by every API call in the SPA.
 * - Serializes non-FormData bodies as JSON.
 * - Parses RFC 9457 Problem Details on non-2xx responses and throws ApiError.
 * - Never logs or persists secrets; response bodies are not inspected here
 *   beyond structural parsing.
 */
export async function apiFetch<TResponse>(
  path: string,
  init: ApiRequestInit = {},
): Promise<TResponse> {
  const { body, headers, ...rest } = init;
  const isFormData = body instanceof FormData;

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...rest,
    headers: {
      Accept: 'application/json',
      [CORRELATION_HEADER_NAME]: crypto.randomUUID(),
      ...(isFormData ? {} : { 'Content-Type': 'application/json' }),
      ...headers,
    },
    body: body === undefined ? undefined : isFormData ? (body as FormData) : JSON.stringify(body),
  });

  if (response.status === 204) {
    return undefined as TResponse;
  }

  const contentType = response.headers.get('content-type') ?? '';
  const isJson =
    contentType.includes('application/json') || contentType.includes('application/problem+json');
  const payload = isJson ? await response.json().catch(() => undefined) : undefined;

  if (!response.ok) {
    const problem: ProblemDetails = (payload as ProblemDetails) ?? {
      title: response.statusText || 'Request failed',
      status: response.status,
    };
    throw new ApiError(problem);
  }

  return payload as TResponse;
}

/**
 * TASK-KNW-06: variant of `apiFetch` for endpoints that return plain text
 * (the knowledge-file preview endpoint) instead of JSON. Mirrors the same
 * credentials/401/error-parsing conventions as `apiFetch`; only the success
 * path differs (returns `response.text()` instead of parsing JSON).
 */
export async function apiFetchText(path: string, init: ApiRequestInit = {}): Promise<string> {
  const { body, headers, ...rest } = init;

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...rest,
    headers: {
      Accept: 'text/plain',
      [CORRELATION_HEADER_NAME]: crypto.randomUUID(),
      ...headers,
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  if (!response.ok) {
    const contentType = response.headers.get('content-type') ?? '';
    const isJson =
      contentType.includes('application/problem+json') || contentType.includes('application/json');
    const payload = isJson ? await response.json().catch(() => undefined) : undefined;
    const problem: ProblemDetails = (payload as ProblemDetails) ?? {
      title: response.statusText || 'Request failed',
      status: response.status,
    };
    throw new ApiError(problem);
  }

  return response.text();
}

export const apiClient = {
  get: <T>(path: string, init?: ApiRequestInit) => apiFetch<T>(path, { ...init, method: 'GET' }),
  getText: (path: string, init?: ApiRequestInit) => apiFetchText(path, { ...init, method: 'GET' }),
  post: <T>(path: string, body?: unknown, init?: ApiRequestInit) =>
    apiFetch<T>(path, { ...init, method: 'POST', body }),
  put: <T>(path: string, body?: unknown, init?: ApiRequestInit) =>
    apiFetch<T>(path, { ...init, method: 'PUT', body }),
  patch: <T>(path: string, body?: unknown, init?: ApiRequestInit) =>
    apiFetch<T>(path, { ...init, method: 'PATCH', body }),
  delete: <T>(path: string, init?: ApiRequestInit) =>
    apiFetch<T>(path, { ...init, method: 'DELETE' }),
};
