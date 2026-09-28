import { render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  motionDurationsSeconds,
  buttonFeedbackTransition,
  popoverTransition,
  drawerTransition,
  pageContentTransition,
  statusTransition,
} from '../../lib/motion/presets';
import { useReducedMotionPreference } from '../../lib/motion/useReducedMotionPreference';
import { CollapsibleTransition, StatusFade } from '../../components/motion/transitions';

function ReducedMotionProbe() {
  const reduced = useReducedMotionPreference();
  return <span data-testid="reduced-motion-value">{String(reduced)}</span>;
}

function mockMatchMedia(matches: boolean) {
  vi.stubGlobal(
    'matchMedia',
    vi.fn().mockImplementation((query: string) => ({
      matches,
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  );
}

describe('motion presets', () => {
  it('stay within the ASDD §7 timing budgets', () => {
    expect(motionDurationsSeconds.buttonFeedback).toBeGreaterThanOrEqual(0.1);
    expect(motionDurationsSeconds.buttonFeedback).toBeLessThanOrEqual(0.15);

    expect(motionDurationsSeconds.popover).toBeGreaterThanOrEqual(0.15);
    expect(motionDurationsSeconds.popover).toBeLessThanOrEqual(0.2);

    expect(motionDurationsSeconds.drawer).toBeGreaterThanOrEqual(0.2);
    expect(motionDurationsSeconds.drawer).toBeLessThanOrEqual(0.3);

    expect(motionDurationsSeconds.pageContent).toBeGreaterThanOrEqual(0.2);
    expect(motionDurationsSeconds.pageContent).toBeLessThanOrEqual(0.35);

    expect(motionDurationsSeconds.statusTransition).toBeGreaterThanOrEqual(0.2);
    expect(motionDurationsSeconds.statusTransition).toBeLessThanOrEqual(0.3);
  });

  it('are applied to the shared transition objects', () => {
    expect(buttonFeedbackTransition.duration).toBe(motionDurationsSeconds.buttonFeedback);
    expect(popoverTransition.duration).toBe(motionDurationsSeconds.popover);
    expect(drawerTransition.duration).toBe(motionDurationsSeconds.drawer);
    expect(pageContentTransition.duration).toBe(motionDurationsSeconds.pageContent);
    expect(statusTransition.duration).toBe(motionDurationsSeconds.statusTransition);
  });
});

describe('useReducedMotionPreference', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  // Note: motion-dom lazily caches the OS reduced-motion query result once
  // per module lifetime (it only re-queries via a change listener, not on
  // every render), so only one matchMedia scenario can be meaningfully
  // asserted per test process - this covers the acceptance-critical branch
  // ("given reduced-motion preference, when set, then animation is
  // disabled").
  it('reflects prefers-reduced-motion: reduce', () => {
    mockMatchMedia(true);
    render(<ReducedMotionProbe />);
    expect(screen.getByTestId('reduced-motion-value')).toHaveTextContent('true');
  });
});

describe('CollapsibleTransition', () => {
  it('renders children only while open', () => {
    const { rerender } = render(
      <CollapsibleTransition open={false}>
        <p>panel content</p>
      </CollapsibleTransition>,
    );
    expect(screen.queryByText('panel content')).not.toBeInTheDocument();

    rerender(
      <CollapsibleTransition open={true}>
        <p>panel content</p>
      </CollapsibleTransition>,
    );
    expect(screen.getByText('panel content')).toBeInTheDocument();
  });
});

describe('StatusFade', () => {
  it('renders the current status content', () => {
    render(<StatusFade statusKey="processing">Processing</StatusFade>);
    expect(screen.getByText('Processing')).toBeInTheDocument();
  });
});
