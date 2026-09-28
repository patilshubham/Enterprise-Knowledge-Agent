import type { ReactNode } from 'react';
import { MoreHorizontal } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { cn } from '@/lib/utils';

export interface DataTableRowAction {
  /** Unique key within the menu (used as the React key). */
  id: string;
  label: string;
  icon?: ReactNode;
  onSelect: () => void;
  /** Renders the item in the destructive (danger) color and adds a separator above it. */
  destructive?: boolean;
  disabled?: boolean;
}

interface DataTableRowActionsProps {
  actions: DataTableRowAction[];
  /** Accessible label for the trigger button; defaults to "Row actions". */
  label?: string;
}

/**
 * TASK-UX-06: consistent per-row action menu (ellipsis trigger -> dropdown).
 * Destructive actions (delete/archive/etc.) always render below a separator
 * in the danger color rather than inline in the row, per the shared
 * data-table pattern's requirement that destructive actions live in a row
 * menu, not inline.
 */
export function DataTableRowActions({ actions, label = 'Row actions' }: DataTableRowActionsProps) {
  if (actions.length === 0) {
    return null;
  }

  const standardActions = actions.filter((action) => !action.destructive);
  const destructiveActions = actions.filter((action) => action.destructive);

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="ghost"
          size="icon"
          className="size-8"
          aria-label={label}
          onClick={(event) => event.stopPropagation()}
        >
          <MoreHorizontal className="size-4" aria-hidden="true" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" onClick={(event) => event.stopPropagation()}>
        {standardActions.map((action) => (
          <DropdownMenuItem key={action.id} disabled={action.disabled} onSelect={action.onSelect}>
            {action.icon}
            {action.label}
          </DropdownMenuItem>
        ))}
        {standardActions.length > 0 && destructiveActions.length > 0 && <DropdownMenuSeparator />}
        {destructiveActions.map((action) => (
          <DropdownMenuItem
            key={action.id}
            disabled={action.disabled}
            onSelect={action.onSelect}
            className={cn('text-danger focus:bg-danger/10 focus:text-danger')}
          >
            {action.icon}
            {action.label}
          </DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
