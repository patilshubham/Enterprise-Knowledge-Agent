import {
  Bot,
  FileOutput,
  FolderKanban,
  History,
  Info,
  LayoutDashboard,
  LayoutGrid,
  MessageSquare,
  PlayCircle,
  ScrollText,
  Server,
  type LucideIcon,
} from 'lucide-react';
import type { Capabilities } from '@/app/authz/capabilities';

export interface NavItem {
  label: string;
  path: string;
  icon: LucideIcon;
  /** Returns whether this item is visible for the given (UX-only) capabilities. */
  isVisible: (caps: Capabilities) => boolean;
}

/**
 * TASK-UX-04: static Platform-section nav. Visibility here is a UX
 * convenience only (FR-3.5) - the server independently enforces every
 * action regardless of what the sidebar shows.
 */
export const platformNavItems: NavItem[] = [
  { label: 'Dashboard', path: '/platform/dashboard', icon: LayoutDashboard, isVisible: () => true },
  {
    label: 'Applications',
    path: '/platform/applications',
    icon: LayoutGrid,
    isVisible: () => true,
  },
  {
    label: 'Processing',
    path: '/platform/processing',
    icon: Server,
    isVisible: (caps) => caps.canViewPlatformProcessing,
  },
  {
    label: 'Audit',
    path: '/platform/audit',
    icon: ScrollText,
    isVisible: (caps) => caps.canViewAudit,
  },
  {
    label: 'Operations',
    path: '/platform/operations',
    icon: FolderKanban,
    isVisible: (caps) => caps.canViewOperations,
  },
];

/**
 * Current-Application-section nav. `path` is a template with `:applicationId`
 * that callers substitute with the active application id.
 */
export const applicationNavItems: NavItem[] = [
  {
    label: 'Overview',
    path: '/applications/:applicationId/overview',
    icon: Info,
    isVisible: (caps) => caps.canViewApplication,
  },
  {
    label: 'Chat',
    path: '/applications/:applicationId/chat',
    icon: MessageSquare,
    isVisible: (caps) => caps.canViewApplication,
  },
  {
    label: 'Knowledge',
    path: '/applications/:applicationId/knowledge',
    icon: FolderKanban,
    isVisible: (caps) => caps.canViewApplication,
  },
  {
    label: 'Processing',
    path: '/applications/:applicationId/processing',
    icon: Server,
    isVisible: (caps) => caps.canViewApplication,
  },
  {
    label: 'Agents',
    path: '/applications/:applicationId/agents',
    icon: Bot,
    isVisible: (caps) => caps.canViewApplication,
  },
  {
    label: 'Runs',
    path: '/applications/:applicationId/runs',
    icon: PlayCircle,
    isVisible: (caps) => caps.canViewApplication,
  },
  {
    label: 'Outputs',
    path: '/applications/:applicationId/outputs',
    icon: FileOutput,
    isVisible: (caps) => caps.canViewApplication && caps.canViewOutput,
  },
  {
    label: 'Activity',
    path: '/applications/:applicationId/activity',
    icon: History,
    // TASK-AUD-03: the `/activity` endpoint enforces `CanViewAudit`, which is
    // platform-roles-only (no per-application variant) - `canViewApplication`
    // alone would let application-scoped members see a link that always 403s.
    isVisible: (caps) => caps.canViewApplication && caps.canViewAudit,
  },
];

export function resolveApplicationPath(template: string, applicationId: string): string {
  return template.replace(':applicationId', applicationId);
}
