import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { EvidenceDrawer } from '@/components/evidence/EvidenceDrawer';
import type { EvidenceSource } from '@/components/evidence/EvidenceDrawer';

const sources: EvidenceSource[] = [
  {
    id: '1',
    fileName: 'Onboarding Guide.md',
    headingPath: 'Onboarding Guide > Access Requests',
    passage: 'Submit an access request through the portal.',
  },
  {
    id: '2',
    fileName: 'Deleted Policy.md',
    passage: '',
    deleted: true,
  },
];

describe('EvidenceDrawer (TASK-UX-10)', () => {
  it('lists every source with its filename and heading path', () => {
    render(<EvidenceDrawer open onOpenChange={vi.fn()} sources={sources} />);

    expect(screen.getByText('Onboarding Guide.md')).toBeInTheDocument();
    expect(screen.getByText('Onboarding Guide > Access Requests')).toBeInTheDocument();
    expect(screen.getByText('Submit an access request through the portal.')).toBeInTheDocument();
  });

  it('shows a graceful fallback for a deleted source (TASK-CIT-04)', () => {
    render(<EvidenceDrawer open onOpenChange={vi.fn()} sources={sources} />);
    expect(screen.getByText('This source is no longer available.')).toBeInTheDocument();
  });

  it('highlights the source matching activeSourceId', () => {
    render(<EvidenceDrawer open onOpenChange={vi.fn()} sources={sources} activeSourceId="1" />);
    expect(screen.getByTestId('evidence-source-1')).toHaveClass('bg-accent/15');
    expect(screen.getByTestId('evidence-source-2')).not.toHaveClass('bg-accent/15');
  });

  it('scrolls the active source into view', () => {
    const scrollIntoView = vi.fn();
    Element.prototype.scrollIntoView = scrollIntoView;

    render(<EvidenceDrawer open onOpenChange={vi.fn()} sources={sources} activeSourceId="1" />);
    expect(scrollIntoView).toHaveBeenCalledWith({ block: 'center' });
  });

  it('does not render drawer content when closed', () => {
    render(<EvidenceDrawer open={false} onOpenChange={vi.fn()} sources={sources} />);
    expect(screen.queryByText('Onboarding Guide.md')).not.toBeInTheDocument();
  });
});
