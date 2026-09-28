import { Link, useNavigate, useParams } from 'react-router-dom';
import { Check, ChevronsUpDown, LayoutGrid } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { cn } from '@/lib/utils';
import { chipStyle } from '@/components/patterns/tone';
import { useApplications } from './useApplications';

/**
 * Redesign: a real application switcher. Shows the current application's NAME
 * (never the raw GUID) with a brand chip, and a dropdown to jump straight to
 * any application the caller can see, plus a link to browse them all.
 */
export function AppSwitcher({ collapsed }: { collapsed: boolean }) {
  const { applicationId } = useParams<{ applicationId?: string }>();
  const navigate = useNavigate();
  const { data: applications } = useApplications();

  const current = applications?.find((application) => application.id === applicationId);
  const label = current?.name ?? (applicationId ? 'Loading…' : 'All applications');

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="ghost"
          className={cn(
            'flex h-auto w-full items-center gap-2 rounded-control px-2 py-2 hover:bg-surface-hover',
            collapsed ? 'justify-center' : 'justify-start',
          )}
          aria-label="Switch application"
        >
          <span className="icon-chip size-8 shrink-0" style={chipStyle('accent')} aria-hidden="true">
            <LayoutGrid className="size-4" />
          </span>
          {!collapsed && (
            <span className="flex min-w-0 flex-1 flex-col items-start leading-tight">
              <span className="text-[10px] font-semibold uppercase tracking-wider text-text-muted">
                Application
              </span>
              <span className="w-full truncate text-left text-sm font-medium text-text-primary">
                {label}
              </span>
            </span>
          )}
          {!collapsed && <ChevronsUpDown className="size-3.5 shrink-0 text-text-muted" />}
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" className="w-64">
        <DropdownMenuLabel>Switch application</DropdownMenuLabel>
        <DropdownMenuSeparator />
        {applications && applications.length > 0 ? (
          applications.slice(0, 8).map((application) => (
            <DropdownMenuItem
              key={application.id}
              onSelect={() => navigate(`/applications/${application.id}/overview`)}
              className="gap-2"
            >
              <Check
                className={cn(
                  'size-4 shrink-0',
                  application.id === applicationId ? 'text-accent opacity-100' : 'opacity-0',
                )}
              />
              <span className="min-w-0 flex-1 truncate">{application.name}</span>
            </DropdownMenuItem>
          ))
        ) : (
          <DropdownMenuItem disabled>No applications yet</DropdownMenuItem>
        )}
        <DropdownMenuSeparator />
        <DropdownMenuItem asChild>
          <Link to="/platform/applications">Browse all applications</Link>
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
