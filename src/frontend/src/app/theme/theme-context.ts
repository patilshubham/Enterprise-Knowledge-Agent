import { createContext, useContext } from 'react';

/**
 * TASK-UX-01 (Module 37): context/hook split into its own module so
 * `ThemeProvider.tsx` only exports the component (react-refresh's
 * only-export-components rule requires this for Fast Refresh to work).
 */

export type Theme = 'dark' | 'light';

export interface ThemeContextValue {
  theme: Theme;
  setTheme: (theme: Theme) => void;
  toggleTheme: () => void;
}

export const ThemeContext = createContext<ThemeContextValue | undefined>(undefined);

export function useTheme(): ThemeContextValue {
  const context = useContext(ThemeContext);
  if (!context) {
    throw new Error('useTheme must be used within a ThemeProvider');
  }
  return context;
}
