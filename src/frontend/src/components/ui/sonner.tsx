import type { CSSProperties } from 'react';
import { Toaster as SonnerToaster, type ToasterProps } from 'sonner';
import { useTheme } from '@/app/theme/theme-context';

/**
 * TASK-UX-02: Sonner toaster configured once at the app root (see App.tsx)
 * and wired to TASK-UX-01's ThemeProvider + CSS tokens so toasts always
 * match the active light/dark theme instead of Sonner's own default
 * palette.
 */
function Toaster(props: ToasterProps) {
  const { theme } = useTheme();

  return (
    <SonnerToaster
      theme={theme}
      className="toaster group"
      style={
        {
          '--normal-bg': 'var(--surface-raised)',
          '--normal-text': 'var(--text-primary)',
          '--normal-border': 'var(--border)',
          '--success-bg': 'var(--surface-raised)',
          '--success-text': 'var(--success)',
          '--success-border': 'var(--border)',
          '--error-bg': 'var(--surface-raised)',
          '--error-text': 'var(--danger)',
          '--error-border': 'var(--border)',
          '--warning-bg': 'var(--surface-raised)',
          '--warning-text': 'var(--warning)',
          '--warning-border': 'var(--border)',
        } as CSSProperties
      }
      {...props}
    />
  );
}

export { Toaster };
