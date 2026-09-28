/**
 * API base URL for every fetch call in the SPA. Empty string means
 * same-origin, relying on the Vite dev proxy (local dev) or the nginx
 * same-origin proxy (compose) to forward `/api` and `/hubs` to the API
 * service - see TASK-FND-06. This keeps cookie auth + CSRF + SameSite
 * simple (D7) and avoids CORS entirely.
 */
export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '';
