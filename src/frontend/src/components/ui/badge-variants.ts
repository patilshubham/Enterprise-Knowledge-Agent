import { cva } from 'class-variance-authority';

/**
 * Split from badge.tsx solely so badge.tsx can export only the `Badge`
 * component (react-refresh/only-export-components requires component-only
 * files for Fast Refresh to work).
 *
 * Status-color variants (success/warning/processing/destructive) exist
 * alongside the neutral ones because ASDD/UX-01 require status to never be
 * conveyed by color alone - consuming code must still pair a Badge with an
 * icon and/or text label, this component only supplies the color role.
 */
const badgeVariants = cva(
  'inline-flex w-fit shrink-0 items-center justify-center gap-1 whitespace-nowrap rounded-full border px-2 py-0.5 text-xs font-medium transition-colors [&_svg]:pointer-events-none [&_svg]:size-3',
  {
    variants: {
      variant: {
        default: 'border-transparent bg-primary text-primary-foreground',
        secondary: 'border-transparent bg-secondary text-secondary-foreground',
        destructive: 'border-transparent bg-destructive text-destructive-foreground',
        success: 'border-transparent bg-success/15 text-success',
        warning: 'border-transparent bg-warning/15 text-warning',
        processing: 'border-transparent bg-processing/15 text-processing',
        outline: 'border-border text-text-primary',
      },
    },
    defaultVariants: {
      variant: 'default',
    },
  },
);

export { badgeVariants };
