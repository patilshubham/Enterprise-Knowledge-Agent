/**
 * TASK-UX-03 (Module 37): central Motion timing/easing presets (ASDD §7,
 * decision D15). Never hand-roll a `duration`/`ease` value at a call site -
 * import from here so every transition in the app stays within the defined
 * timing budget:
 *   - button feedback   100-150ms
 *   - popover           150-200ms
 *   - drawer/sheet       200-300ms
 *   - page content       200-350ms
 *   - status transition  200-300ms
 *
 * Animation exists to explain STATE (queued -> processing -> ready) and
 * spatial origin (drawers sliding from their anchor edge) - never as
 * decorative background movement. It is intentionally NOT used in tables,
 * forms, audit trails, or error/security screens (see transitions.tsx).
 */

export const motionDurationsSeconds = {
  buttonFeedback: 0.13,
  popover: 0.18,
  drawer: 0.26,
  pageContent: 0.28,
  statusTransition: 0.24,
} as const;

export const motionEasings = {
  /** General-purpose UI easing (Material-style standard curve). */
  standard: [0.4, 0, 0.2, 1],
  /** Entrances - starts fast, settles gently. */
  decelerate: [0, 0, 0.2, 1],
  /** Exits - leaves the screen quickly. */
  accelerate: [0.4, 0, 1, 1],
} as const;

export const buttonFeedbackTransition = {
  duration: motionDurationsSeconds.buttonFeedback,
  ease: motionEasings.standard,
};

export const popoverTransition = {
  duration: motionDurationsSeconds.popover,
  ease: motionEasings.decelerate,
};

export const drawerTransition = {
  duration: motionDurationsSeconds.drawer,
  ease: motionEasings.decelerate,
};

export const pageContentTransition = {
  duration: motionDurationsSeconds.pageContent,
  ease: motionEasings.standard,
};

export const statusTransition = {
  duration: motionDurationsSeconds.statusTransition,
  ease: motionEasings.standard,
};
