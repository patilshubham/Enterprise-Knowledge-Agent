/**
 * There is no login or user management in this app (every visitor has the
 * same, full access) - this module only still exists so the UI code that
 * used to gate nav items/actions on role-derived flags doesn't need to be
 * rewritten everywhere. Every flag is always `true`.
 */
export interface Capabilities {
  isSuperAdmin: boolean;
  isSuperViewer: boolean;
  canManageApplications: boolean;
  canViewApplication: boolean;
  canManageKnowledge: boolean;
  canManageAgents: boolean;
  canExecuteAgent: boolean;
  canViewFullPrompts: boolean;
  canManageOutput: boolean;
  canViewOutput: boolean;
  canViewPlatformProcessing: boolean;
  canViewAudit: boolean;
  canViewOperations: boolean;
}

export const FULL_CAPABILITIES: Capabilities = {
  isSuperAdmin: true,
  isSuperViewer: true,
  canManageApplications: true,
  canViewApplication: true,
  canManageKnowledge: true,
  canManageAgents: true,
  canExecuteAgent: true,
  canViewFullPrompts: true,
  canManageOutput: true,
  canViewOutput: true,
  canViewPlatformProcessing: true,
  canViewAudit: true,
  canViewOperations: true,
};
