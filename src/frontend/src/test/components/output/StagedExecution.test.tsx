import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { StagedExecution, EXECUTION_STAGES } from '@/components/output/StagedExecution';

function statusTextFor(label: string) {
  return screen.getByText(label).querySelector('.sr-only')?.textContent;
}

describe('StagedExecution (TASK-UX-10)', () => {
  it('renders all six stages, not a bare spinner', () => {
    render(<StagedExecution currentStage="generating" />);
    for (const stage of EXECUTION_STAGES) {
      expect(screen.getByText(stage.label)).toBeInTheDocument();
    }
  });

  it('marks stages before currentStage as done and the current stage as active', () => {
    render(<StagedExecution currentStage="selecting" />);
    expect(statusTextFor('Request accepted')).toBe(' - Completed');
    expect(statusTextFor('Sections searched')).toBe(' - Completed');
    expect(statusTextFor('Passages selected')).toBe(' - In progress');
    expect(statusTextFor('Generating')).toBe(' - Pending');
  });

  it('marks the failed stage as failed and stops progress there', () => {
    render(<StagedExecution currentStage="saving" failedStage="generating" />);
    expect(statusTextFor('Passages selected')).toBe(' - Completed');
    expect(statusTextFor('Generating')).toBe(' - Failed');
    expect(statusTextFor('Validating citations')).toBe(' - Pending');
    expect(statusTextFor('Saving')).toBe(' - Pending');
  });
});
