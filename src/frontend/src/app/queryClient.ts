import { QueryClient } from '@tanstack/react-query';

/**
 * Default retry policy avoids hammering the API on 4xx (client) errors -
 * only network/5xx failures are retried, and at most twice. SignalR events
 * (wired in TASK-SIG-06) invalidate affected queries for live updates; REST
 * responses remain authoritative after reconnect (ASDD §7.3).
 */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: (failureCount, error) => {
        const status = (error as { status?: number })?.status;
        if (status !== undefined && status >= 400 && status < 500) {
          return false;
        }
        return failureCount < 2;
      },
      refetchOnWindowFocus: false,
    },
  },
});
