import { useState } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import { motion } from 'motion/react';
import { Sheet, SheetContent, SheetTitle } from '@/components/ui/sheet';
import { pageContentTransition } from '@/lib/motion/presets';
import { CommandPalette } from './CommandPalette';
import { Header } from './Header';
import { Sidebar, SidebarNav } from './Sidebar';
import { useCommandPaletteShortcut } from './useCommandPaletteShortcut';

/**
 * Redesign: gentle entrance for the routed page content. Keyed on the pathname
 * so each navigation fades/slides the new page in (entrance only - no exit, to
 * avoid the data-router "old content flash" and keep it snappy). Respects
 * reduced motion via the global `MotionConfig`.
 */
function RoutedContent() {
  const location = useLocation();
  return (
    <motion.div
      key={location.pathname}
      initial={{ opacity: 0, y: 8 }}
      animate={{ opacity: 1, y: 0 }}
      transition={pageContentTransition}
      className="h-full"
    >
      <Outlet />
    </motion.div>
  );
}

/**
 * TASK-UX-04: global application shell. Replaces `RootLayout`'s bare
 * `<Outlet/>` with the collapsible sidebar + header described in ASDD
 * §7.1/§7.2. Nav gating (`useCapabilities`) lives in `Sidebar`/`SidebarNav`
 * so both the desktop rail and the mobile drawer share one source of truth.
 * Also hosts the TASK-UX-05 command palette (Ctrl/Cmd+K).
 */
export function AppShell() {
  const [collapsed, setCollapsed] = useState(false);
  const [mobileNavOpen, setMobileNavOpen] = useState(false);
  const [commandPaletteOpen, setCommandPaletteOpen] = useState(false);

  useCommandPaletteShortcut(() => setCommandPaletteOpen((value) => !value));

  return (
    <div className="flex h-screen overflow-hidden bg-background text-text-primary">
      <Sidebar collapsed={collapsed} onToggleCollapsed={() => setCollapsed((value) => !value)} />

      <Sheet open={mobileNavOpen} onOpenChange={setMobileNavOpen}>
        <SheetContent side="left" className="w-72 p-0">
          <SheetTitle className="sr-only">Navigation</SheetTitle>
          <SidebarNav collapsed={false} />
        </SheetContent>
      </Sheet>

      <CommandPalette open={commandPaletteOpen} onOpenChange={setCommandPaletteOpen} />

      <div className="flex min-w-0 flex-1 flex-col">
        <Header
          onOpenMobileNav={() => setMobileNavOpen(true)}
          onOpenCommandPalette={() => setCommandPaletteOpen(true)}
        />
        <main className="app-backdrop flex-1 overflow-y-auto p-6">
          <RoutedContent />
        </main>
      </div>
    </div>
  );
}

