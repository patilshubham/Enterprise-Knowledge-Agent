import { useReducedMotion } from 'motion/react';

/**
 * TASK-UX-03: thin wrapper around Motion's own `useReducedMotion` so app
 * code imports the reduced-motion check from this module (consistent with
 * the other motion utilities here) instead of reaching into `motion/react`
 * directly. Returns `true` when the user has `prefers-reduced-motion:
 * reduce` set (or Motion's internal force-reduced-motion flag is active).
 *
 * Most components don't need this directly - wrap animated content in
 * `MotionProvider` (root-level `MotionConfig reducedMotion="user"`) and
 * every Motion-driven animation resolves to its end state automatically.
 * Use this hook only when a component needs to branch its OWN logic
 * (e.g. skip a staggered list entrance entirely) rather than just let
 * Motion collapse the animation.
 */
export function useReducedMotionPreference(): boolean {
  return useReducedMotion() ?? false;
}
