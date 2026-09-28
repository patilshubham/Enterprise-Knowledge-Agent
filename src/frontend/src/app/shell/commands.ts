import type { NavigateFunction } from 'react-router-dom';
import type { Capabilities } from '@/app/authz/capabilities';

export interface CommandRunContext {
  navigate: NavigateFunction;
  applicationId: string | null;
  toggleTheme: () => void;
}

export interface CommandDefinition {
  id: string;
  label: string;
  group: 'Applications' | 'Current application' | 'Navigate' | 'Preferences';
  isVisible: (capabilities: Capabilities, applicationId: string | null) => boolean;
  run: (ctx: CommandRunContext) => void;
}

/**
 * TASK-UX-05: registry of Ctrl/Cmd+K commands. Each command is filtered by
 * `useCapabilities` (never shown-then-denied) - the server still enforces
 * every action regardless (FR-3.5). Commands that would open a creation
 * flow not yet built by their owning module (create application/agent,
 * upload knowledge) navigate to that module's list page instead, where the
 * real action will live once implemented.
 */
export const commandRegistry: CommandDefinition[] = [
  {
    id: 'create-application',
    label: 'Create application',
    group: 'Applications',
    isVisible: (caps) => caps.isSuperAdmin,
    run: ({ navigate }) => navigate('/platform/applications'),
  },
  {
    id: 'switch-application',
    label: 'Switch application',
    group: 'Applications',
    isVisible: () => true,
    run: ({ navigate }) => navigate('/platform/applications'),
  },
  {
    id: 'upload-knowledge',
    label: 'Upload knowledge',
    group: 'Current application',
    isVisible: (caps, applicationId) => Boolean(applicationId) && caps.canManageKnowledge,
    run: ({ navigate, applicationId }) => navigate(`/applications/${applicationId}/knowledge`),
  },
  {
    id: 'create-agent',
    label: 'Create agent',
    group: 'Current application',
    isVisible: (caps, applicationId) => Boolean(applicationId) && caps.canManageAgents,
    run: ({ navigate, applicationId }) => navigate(`/applications/${applicationId}/agents`),
  },
  {
    id: 'run-agent',
    label: 'Run agent',
    group: 'Current application',
    isVisible: (caps, applicationId) => Boolean(applicationId) && caps.canExecuteAgent,
    run: ({ navigate, applicationId }) => navigate(`/applications/${applicationId}/agents`),
  },
  {
    id: 'search-outputs',
    label: 'Search outputs',
    group: 'Current application',
    isVisible: (caps, applicationId) => Boolean(applicationId) && caps.canViewOutput,
    run: ({ navigate, applicationId }) => navigate(`/applications/${applicationId}/outputs`),
  },
  {
    id: 'open-processing',
    label: 'Open processing',
    group: 'Navigate',
    isVisible: (caps, applicationId) =>
      applicationId ? caps.canViewApplication : caps.canViewPlatformProcessing,
    run: ({ navigate, applicationId }) =>
      navigate(
        applicationId ? `/applications/${applicationId}/processing` : '/platform/processing',
      ),
  },
  {
    id: 'view-failed-jobs',
    label: 'View failed jobs',
    group: 'Navigate',
    isVisible: (caps, applicationId) =>
      applicationId ? caps.canViewApplication : caps.canViewPlatformProcessing,
    run: ({ navigate, applicationId }) =>
      navigate(
        applicationId
          ? `/applications/${applicationId}/processing?status=failed`
          : '/platform/processing?status=failed',
      ),
  },
  {
    id: 'open-system-health',
    label: 'Open system health',
    group: 'Navigate',
    isVisible: (caps) => caps.isSuperAdmin || caps.isSuperViewer,
    run: ({ navigate }) => navigate('/platform/operations'),
  },
  {
    id: 'change-theme',
    label: 'Change theme',
    group: 'Preferences',
    isVisible: () => true,
    run: ({ toggleTheme }) => toggleTheme(),
  },
];
