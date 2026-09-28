import { useQuery } from '@tanstack/react-query';
import { fetchReadiness, type ReadinessState } from '@/api/health';

export const READINESS_QUERY_KEY = ['health', 'ready'] as const;

/**
 * TASK-UX-04: polls readiness every 30s for the header's health indicator.
 * Degrades gracefully - a failed/errored probe never throws into the UI,
 * `fetchReadiness` already maps failures to `'unknown'`.
 */
export function useReadiness(): ReadinessState {
  const { data } = useQuery({
    queryKey: READINESS_QUERY_KEY,
    queryFn: fetchReadiness,
    staleTime: 15_000,
    refetchInterval: 30_000,
    retry: false,
  });

  return data ?? 'unknown';
}
