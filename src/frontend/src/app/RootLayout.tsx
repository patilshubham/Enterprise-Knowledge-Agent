import { AppShell } from './shell/AppShell';

/**
 * Root layout for every route (see `router.tsx`). Renders the global
 * application shell built in `TASK-UX-04` - sidebar, header, breadcrumbs,
 * app switcher/nav, health indicator, and responsive mobile drawer.
 */
export function RootLayout() {
  return <AppShell />;
}
