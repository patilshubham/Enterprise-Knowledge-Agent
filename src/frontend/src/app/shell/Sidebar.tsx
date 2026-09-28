import { Link, NavLink, useParams } from 'react-router-dom';
import { PanelLeftClose, PanelLeftOpen, Sparkles } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { useCapabilities } from '@/app/authz/useCapabilities';
import { chipStyle } from '@/components/patterns/tone';
import { AppSwitcher } from './AppSwitcher';
import {
  applicationNavItems,
  platformNavItems,
  resolveApplicationPath,
  type NavItem,
} from './nav-items';

function NavLinkItem({
  item,
  path,
  collapsed,
}: {
  item: NavItem;
  path: string;
  collapsed: boolean;
}) {
  const Icon = item.icon;
  const link = (
    <NavLink
      to={path}
      className={({ isActive }) =>
        cn(
          'group relative flex items-center gap-3 rounded-control px-3 py-2 text-sm font-medium text-text-secondary transition-colors hover:bg-surface-hover hover:text-text-primary',
          isActive &&
            'bg-accent/12 text-text-primary ring-1 ring-inset ring-accent/25 hover:bg-accent/12',
          collapsed && 'justify-center px-2',
        )
      }
    >
      {({ isActive }) => (
        <>
          {isActive && !collapsed && (
            <span
              className="absolute left-0 top-1/2 h-4 w-0.5 -translate-y-1/2 rounded-full bg-accent"
              aria-hidden="true"
            />
          )}
          <Icon
            className={cn(
              'size-4 shrink-0 transition-colors',
              isActive ? 'text-accent' : 'text-text-muted group-hover:text-text-secondary',
            )}
            aria-hidden="true"
          />
          {!collapsed && <span className="truncate">{item.label}</span>}
        </>
      )}
    </NavLink>
  );

  if (!collapsed) {
    return link;
  }

  return (
    <Tooltip>
      <TooltipTrigger asChild>{link}</TooltipTrigger>
      <TooltipContent side="right">{item.label}</TooltipContent>
    </Tooltip>
  );
}

/**
 * Nav content shared by the desktop sidebar and the mobile drawer
 * (`AppShell`'s `Sheet`). `collapsed` only ever applies to the desktop
 * rendering - the mobile drawer always shows full labels.
 */
export function SidebarNav({ collapsed }: { collapsed: boolean }) {
  const { applicationId } = useParams<{ applicationId?: string }>();
  const capabilities = useCapabilities(applicationId ?? null);

  const visiblePlatformItems = platformNavItems.filter((item) => item.isVisible(capabilities));
  const visibleApplicationItems = applicationId
    ? applicationNavItems.filter((item) => item.isVisible(capabilities))
    : [];

  return (
    <nav aria-label="Primary" className="flex flex-col gap-4 p-2">
      <AppSwitcher collapsed={collapsed} />

      <div className="flex flex-col gap-1">
        {!collapsed && (
          <p className="px-3 pb-1 text-xs font-semibold tracking-wide text-text-muted uppercase">
            Platform
          </p>
        )}
        {visiblePlatformItems.map((item) => (
          <NavLinkItem key={item.path} item={item} path={item.path} collapsed={collapsed} />
        ))}
      </div>

      {applicationId && visibleApplicationItems.length > 0 && (
        <div className="flex flex-col gap-1 border-t border-border pt-3">
          {!collapsed && (
            <p className="px-3 pb-1 text-xs font-semibold tracking-wide text-text-muted uppercase">
              Current Application
            </p>
          )}
          {visibleApplicationItems.map((item) => (
            <NavLinkItem
              key={item.path}
              item={item}
              path={resolveApplicationPath(item.path, applicationId)}
              collapsed={collapsed}
            />
          ))}
        </div>
      )}
    </nav>
  );
}

interface SidebarProps {
  collapsed: boolean;
  onToggleCollapsed: () => void;
}

/** Desktop-only sidebar (hidden below `md`; see `AppShell`'s mobile drawer for narrow viewports). */
export function Sidebar({ collapsed, onToggleCollapsed }: SidebarProps) {
  return (
    <aside
      className={cn(
        'hidden shrink-0 border-r border-border bg-gradient-to-b from-surface to-background md:flex md:flex-col',
        collapsed ? 'md:w-16' : 'md:w-60',
      )}
    >
      <div className="flex items-center justify-between gap-2 p-3">
        {!collapsed ? (
          <Link to="/platform/dashboard" className="flex min-w-0 items-center gap-2">
            <span className="icon-chip size-8 shrink-0" style={chipStyle('accent')} aria-hidden="true">
              <Sparkles className="size-4" />
            </span>
            <span className="truncate text-sm font-semibold tracking-tight text-text-primary">
              Enterprise<span className="text-text-muted"> KA</span>
            </span>
          </Link>
        ) : (
          <Link to="/platform/dashboard" aria-label="Home" className="mx-auto">
            <span className="icon-chip size-8 shrink-0" style={chipStyle('accent')} aria-hidden="true">
              <Sparkles className="size-4" />
            </span>
          </Link>
        )}
        {!collapsed && (
          <Button
            variant="ghost"
            size="icon"
            aria-label="Collapse sidebar"
            onClick={onToggleCollapsed}
          >
            <PanelLeftClose className="size-4" aria-hidden="true" />
          </Button>
        )}
      </div>
      {collapsed && (
        <div className="flex justify-center pb-1">
          <Button
            variant="ghost"
            size="icon"
            aria-label="Expand sidebar"
            onClick={onToggleCollapsed}
          >
            <PanelLeftOpen className="size-4" aria-hidden="true" />
          </Button>
        </div>
      )}
      <div className="flex-1 overflow-y-auto">
        <SidebarNav collapsed={collapsed} />
      </div>
    </aside>
  );
}
