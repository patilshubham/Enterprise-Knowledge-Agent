import { useQuery } from '@tanstack/react-query';
import { applicationsQueryKeys, listApplications } from '@/api/applications';

/**
 * Redesign: shared, cached lookup of the applications the caller can see. Used
 * by the app switcher and breadcrumbs so the shell shows real application
 * NAMES instead of raw GUIDs. One query key means a single cached fetch shared
 * across the whole shell.
 */
export function useApplications() {
  return useQuery({
    queryKey: applicationsQueryKeys.list(),
    queryFn: listApplications,
    staleTime: 60_000,
  });
}

/**
 * Resolves an application id to its display name (falling back to a shortened
 * id while the list loads or if it isn't visible to the caller).
 */
export function useApplicationName(applicationId: string | undefined): string | undefined {
  const { data } = useApplications();
  if (!applicationId) {
    return undefined;
  }
  const match = data?.find((application) => application.id === applicationId);
  if (match) {
    return match.name;
  }
  // Graceful fallback while the list loads (or if it isn't visible to the
  // caller): a neutral word rather than a raw GUID fragment.
  return 'Application';
}
