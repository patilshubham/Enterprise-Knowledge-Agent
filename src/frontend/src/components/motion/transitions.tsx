import type { ReactNode } from 'react';
import { AnimatePresence, motion } from 'motion/react';
import {
  drawerTransition,
  pageContentTransition,
  popoverTransition,
  statusTransition,
} from '@/lib/motion/presets';

/**
 * TASK-UX-03 (Module 37): shared, purposeful transition wrappers built on
 * the timing/easing presets in presets.ts.
 *
 * PROHIBITED usage (ASDD §7, decision D15): do not use these (or any other
 * decorative animation) in data tables, forms, audit trails, or
 * error/security screens - state there must be conveyed instantly and
 * unambiguously, never via motion.
 */

interface FadeSlideInProps {
  children: ReactNode;
  className?: string;
  /** Which timing budget this entrance belongs to. */
  variant?: 'popover' | 'drawer' | 'page';
}

/** Generic entrance for popovers/drawers/sheets/page content. */
function FadeSlideIn({ children, className, variant = 'page' }: FadeSlideInProps) {
  const transition =
    variant === 'popover'
      ? popoverTransition
      : variant === 'drawer'
        ? drawerTransition
        : pageContentTransition;
  const offset = variant === 'drawer' ? 12 : 6;

  return (
    <motion.div
      className={className}
      initial={{ opacity: 0, y: offset }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, y: offset }}
      transition={transition}
    >
      {children}
    </motion.div>
  );
}

interface TabsContentTransitionProps {
  /** The active tab's value - changing this triggers a crossfade. */
  activeKey: string;
  children: ReactNode;
  className?: string;
}

/** Crossfades tab-panel content when the active tab changes. */
function TabsContentTransition({ activeKey, children, className }: TabsContentTransitionProps) {
  return (
    <AnimatePresence mode="wait" initial={false}>
      <motion.div
        key={activeKey}
        className={className}
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        exit={{ opacity: 0 }}
        transition={popoverTransition}
      >
        {children}
      </motion.div>
    </AnimatePresence>
  );
}

interface CollapsibleTransitionProps {
  open: boolean;
  children: ReactNode;
  className?: string;
}

/** Progressive-disclosure height/opacity transition (e.g. "show more" panels). */
function CollapsibleTransition({ open, children, className }: CollapsibleTransitionProps) {
  return (
    <AnimatePresence initial={false}>
      {open && (
        <motion.div
          key="collapsible-transition-content"
          className={className}
          initial={{ height: 0, opacity: 0 }}
          animate={{ height: 'auto', opacity: 1 }}
          exit={{ height: 0, opacity: 0 }}
          transition={drawerTransition}
          style={{ overflow: 'hidden' }}
        >
          {children}
        </motion.div>
      )}
    </AnimatePresence>
  );
}

const listContainerVariants = {
  hidden: {},
  visible: { transition: { staggerChildren: 0.04 } },
};

const listItemVariants = {
  hidden: { opacity: 0, y: 6 },
  visible: { opacity: 1, y: 0, transition: pageContentTransition },
};

interface ListEntranceProps {
  children: ReactNode;
  className?: string;
}

/** Staggered fade/slide-up entrance for a list container - wrap each row in `ListEntranceItem`. */
function ListEntrance({ children, className }: ListEntranceProps) {
  return (
    <motion.div
      className={className}
      variants={listContainerVariants}
      initial="hidden"
      animate="visible"
    >
      {children}
    </motion.div>
  );
}

function ListEntranceItem({ children, className }: ListEntranceProps) {
  return (
    <motion.div className={className} variants={listItemVariants}>
      {children}
    </motion.div>
  );
}

interface StatusFadeProps {
  /** A key that changes whenever the status changes (e.g. "queued", "processing", "ready"). */
  statusKey: string;
  children: ReactNode;
  className?: string;
}

/** Crossfades a status indicator (icon+text) when its value changes - e.g. queued -> processing -> ready. */
function StatusFade({ statusKey, children, className }: StatusFadeProps) {
  return (
    <AnimatePresence mode="wait" initial={false}>
      <motion.span
        key={statusKey}
        className={className}
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        exit={{ opacity: 0 }}
        transition={statusTransition}
      >
        {children}
      </motion.span>
    </AnimatePresence>
  );
}

export {
  FadeSlideIn,
  TabsContentTransition,
  CollapsibleTransition,
  ListEntrance,
  ListEntranceItem,
  StatusFade,
};
