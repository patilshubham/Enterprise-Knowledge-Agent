import { useEffect } from 'react';

/**
 * TASK-UX-05: global Ctrl/Cmd+K listener that toggles the command palette
 * from anywhere in the app, mirroring the shortcut hint shown in the
 * header's search button.
 */
export function useCommandPaletteShortcut(onToggle: () => void): void {
  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      if (event.key.toLowerCase() === 'k' && (event.metaKey || event.ctrlKey)) {
        event.preventDefault();
        onToggle();
      }
    }

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [onToggle]);
}
