import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { RealtimeConnectionIndicator } from '@/app/shell/RealtimeConnectionIndicator';
import { RealtimeContext, type RealtimeContextValue } from '@/app/realtime/realtime-context';

function renderWithStatus(status: RealtimeContextValue['status']) {
  const value: RealtimeContextValue = {
    status,
    joinApplicationGroup: () => {},
    leaveApplicationGroup: () => {},
    joinRunGroup: () => {},
    leaveRunGroup: () => {},
    joinOperationsGroup: () => {},
    leaveOperationsGroup: () => {},
  };

  return render(
    <RealtimeContext.Provider value={value}>
      <RealtimeConnectionIndicator />
    </RealtimeContext.Provider>,
  );
}

describe('RealtimeConnectionIndicator (TASK-SIG-05)', () => {
  it('renders a colored dot + accessible label for every connection status', () => {
    const expectations: Record<RealtimeContextValue['status'], string> = {
      connecting: 'bg-text-muted',
      connected: 'bg-success',
      reconnecting: 'bg-warning',
      disconnected: 'bg-danger',
    };

    for (const [status, dotClass] of Object.entries(expectations) as Array<
      [RealtimeContextValue['status'], string]
    >) {
      const { unmount } = renderWithStatus(status);
      const dot = screen.getByTestId('realtime-indicator-dot');
      expect(dot).toHaveClass(dotClass);
      expect(dot).toHaveAttribute('data-status', status);
      unmount();
    }
  });

  it('degrades gracefully to disconnected when no SignalRProvider is mounted', () => {
    render(<RealtimeConnectionIndicator />);
    expect(screen.getByTestId('realtime-indicator-dot')).toHaveAttribute(
      'data-status',
      'disconnected',
    );
  });
});
