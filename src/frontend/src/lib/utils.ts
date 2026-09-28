import { type ClassValue, clsx } from 'clsx';
import { twMerge } from 'tailwind-merge';

/**
 * TASK-UX-02 (Module 37): shared class-name helper used by every shadcn/ui
 * primitive (and any bespoke component) - merges conditional Tailwind
 * classes via `clsx` and resolves conflicting utility classes via
 * `tailwind-merge` so consumers can safely override a primitive's default
 * classes without producing duplicate/conflicting utilities.
 */
export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs));
}

/** TASK-KNW-06: formats a byte count as a human-readable size (e.g. "12.3 KB"). */
export function formatFileSize(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} B`;
  }
  const units = ['KB', 'MB', 'GB'];
  let value = bytes / 1024;
  let unitIndex = 0;
  while (value >= 1024 && unitIndex < units.length - 1) {
    value /= 1024;
    unitIndex += 1;
  }
  return `${value.toFixed(1)} ${units[unitIndex]}`;
}
