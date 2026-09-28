/**
 * TASK-AGT-04/FR-PAR-003: named generation-parameter presets. Selecting a
 * preset sets `temperature` to its canonical value; the numeric field
 * remains directly editable afterward (selecting "Custom" or typing a value
 * that doesn't match any preset both resolve to `'Custom'` in
 * `presetForTemperature`).
 */
export type GenerationPreset = 'Precise' | 'Balanced' | 'Creative' | 'Custom';

export const GENERATION_PRESET_TEMPERATURES: Record<Exclude<GenerationPreset, 'Custom'>, number> = {
  Precise: 0.2,
  Balanced: 0.7,
  Creative: 1.1,
};

export const GENERATION_PRESETS: GenerationPreset[] = ['Precise', 'Balanced', 'Creative', 'Custom'];

/** Returns the preset matching `temperature` exactly, or `'Custom'` if it matches none. */
export function presetForTemperature(temperature: number): GenerationPreset {
  const match = (
    Object.entries(GENERATION_PRESET_TEMPERATURES) as [
      Exclude<GenerationPreset, 'Custom'>,
      number,
    ][]
  ).find(([, value]) => value === temperature);
  return match ? match[0] : 'Custom';
}
