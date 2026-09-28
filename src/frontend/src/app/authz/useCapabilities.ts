import { FULL_CAPABILITIES, type Capabilities } from './capabilities';

/**
 * There is no login or user management in this app - this always returns
 * full capabilities. Kept as a hook (accepting, and ignoring, its former
 * per-application/role-derived parameters) so existing call sites don't
 * need to change.
 */
export function useCapabilities(
  _applicationId?: string | null,
  _options?: { viewerExecutionEnabled?: boolean },
): Capabilities {
  return FULL_CAPABILITIES;
}
