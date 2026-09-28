import type { ReactNode } from 'react';
import { MotionConfig } from 'motion/react';

/**
 * TASK-UX-03 (Module 37): global reduced-motion guard, mounted once at the
 * app root (see App.tsx). `reducedMotion="user"` makes every Motion-driven
 * animation anywhere in the app automatically resolve to its end state
 * instantly for users with `prefers-reduced-motion: reduce` set - no need
 * to thread `useReducedMotion` through every animated component by hand.
 *
 * A CSS-level fallback for plain (non-Motion) transitions/animations -
 * e.g. Tailwind's `transition-colors` hover states used across the
 * shadcn-style primitives - lives in index.css.
 */
function MotionProvider({ children }: { children: ReactNode }) {
  return <MotionConfig reducedMotion="user">{children}</MotionConfig>;
}

export { MotionProvider };
