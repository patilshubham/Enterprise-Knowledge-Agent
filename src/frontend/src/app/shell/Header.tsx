import { Menu, Search } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { Breadcrumbs } from './Breadcrumbs';
import { HealthIndicator } from './HealthIndicator';
import { RealtimeConnectionIndicator } from './RealtimeConnectionIndicator';
import { ThemeToggle } from './ThemeToggle';

interface HeaderProps {
  onOpenMobileNav: () => void;
  onOpenCommandPalette: () => void;
}

/**
 * TASK-UX-04: top app header. The command button opens the TASK-UX-05
 * permission-aware command palette (Ctrl/Cmd+K is wired globally in
 * `AppShell`).
 */
export function Header({ onOpenMobileNav, onOpenCommandPalette }: HeaderProps) {
  return (
    <header className="flex h-14 shrink-0 items-center gap-3 border-b border-border bg-surface px-3">
      <Button
        variant="ghost"
        size="icon"
        className="md:hidden"
        aria-label="Open navigation"
        onClick={onOpenMobileNav}
      >
        <Menu className="size-4" aria-hidden="true" />
      </Button>

      <div className="min-w-0 flex-1">
        <Breadcrumbs />
      </div>

      <Tooltip>
        <TooltipTrigger asChild>
          <Button
            variant="outline"
            size="sm"
            className="hidden text-text-secondary sm:inline-flex"
            aria-label="Open command palette"
            onClick={onOpenCommandPalette}
          >
            <Search className="size-4" aria-hidden="true" />
            <span>Search</span>
            <kbd className="ml-2 rounded-control border border-border px-1.5 text-xs text-text-muted">
              ⌘K
            </kbd>
          </Button>
        </TooltipTrigger>
        <TooltipContent side="bottom">Command palette</TooltipContent>
      </Tooltip>

      <RealtimeConnectionIndicator />
      <HealthIndicator />
      <ThemeToggle />
    </header>
  );
}
