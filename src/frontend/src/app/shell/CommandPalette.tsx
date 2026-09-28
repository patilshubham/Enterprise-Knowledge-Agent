import { useMemo } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '@/components/ui/command';
import { useCapabilities } from '@/app/authz/useCapabilities';
import { useTheme } from '@/app/theme/theme-context';
import { commandRegistry, type CommandDefinition } from './commands';

interface CommandPaletteProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/**
 * TASK-UX-05: permission-aware Ctrl/Cmd+K command palette. Commands are
 * filtered by `useCapabilities` before render - unavailable actions are
 * hidden entirely, never shown-then-denied (server still enforces every
 * action regardless, FR-3.5).
 */
export function CommandPalette({ open, onOpenChange }: CommandPaletteProps) {
  const navigate = useNavigate();
  const { applicationId } = useParams<{ applicationId?: string }>();
  const capabilities = useCapabilities(applicationId ?? null);
  const { toggleTheme } = useTheme();

  const groupedCommands = useMemo(() => {
    const visible = commandRegistry.filter((command) =>
      command.isVisible(capabilities, applicationId ?? null),
    );
    const groups = new Map<CommandDefinition['group'], CommandDefinition[]>();
    for (const command of visible) {
      const group = groups.get(command.group) ?? [];
      group.push(command);
      groups.set(command.group, group);
    }
    return groups;
  }, [capabilities, applicationId]);

  function runCommand(command: CommandDefinition) {
    onOpenChange(false);
    command.run({ navigate, applicationId: applicationId ?? null, toggleTheme });
  }

  return (
    <CommandDialog open={open} onOpenChange={onOpenChange}>
      <CommandInput placeholder="Type a command or search..." />
      <CommandList>
        <CommandEmpty>No matching commands.</CommandEmpty>
        {[...groupedCommands.entries()].map(([group, commands]) => (
          <CommandGroup key={group} heading={group}>
            {commands.map((command) => (
              <CommandItem
                key={command.id}
                value={command.label}
                onSelect={() => runCommand(command)}
              >
                {command.label}
              </CommandItem>
            ))}
          </CommandGroup>
        ))}
      </CommandList>
    </CommandDialog>
  );
}
