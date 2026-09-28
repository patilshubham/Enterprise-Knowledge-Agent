import type { ComponentProps } from 'react';
import { Slot } from '@radix-ui/react-slot';
import type { VariantProps } from 'class-variance-authority';
import { cn } from '@/lib/utils';
import { badgeVariants } from '@/components/ui/badge-variants';

/**
 * Status-color variants (success/warning/processing/destructive) exist
 * alongside the neutral ones because ASDD/UX-01 require status to never be
 * conveyed by color alone - consuming code must still pair a Badge with an
 * icon and/or text label, this component only supplies the color role.
 */
interface BadgeProps extends ComponentProps<'span'>, VariantProps<typeof badgeVariants> {
  asChild?: boolean;
}

function Badge({ className, variant, asChild = false, ...props }: BadgeProps) {
  const Comp = asChild ? Slot : 'span';
  return (
    <Comp data-slot="badge" className={cn(badgeVariants({ variant }), className)} {...props} />
  );
}

export { Badge };
export type { BadgeProps };
