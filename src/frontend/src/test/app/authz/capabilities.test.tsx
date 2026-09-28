import { renderHook } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { FULL_CAPABILITIES } from '@/app/authz/capabilities';
import { useCapabilities } from '@/app/authz/useCapabilities';

describe('FULL_CAPABILITIES', () => {
  it('grants every capability - there is no login/user management in this app', () => {
    expect(FULL_CAPABILITIES.canManageApplications).toBe(true);
    expect(FULL_CAPABILITIES.canManageKnowledge).toBe(true);
    expect(FULL_CAPABILITIES.canManageAgents).toBe(true);
    expect(FULL_CAPABILITIES.canExecuteAgent).toBe(true);
    expect(FULL_CAPABILITIES.canViewFullPrompts).toBe(true);
    expect(FULL_CAPABILITIES.canManageOutput).toBe(true);
    expect(FULL_CAPABILITIES.canViewAudit).toBe(true);
    expect(FULL_CAPABILITIES.canViewOperations).toBe(true);
  });
});

describe('useCapabilities (compatibility shim)', () => {
  it('always returns the full-access capabilities regardless of arguments', () => {
    const { result } = renderHook(() => useCapabilities('app-a'));

    expect(result.current).toEqual(FULL_CAPABILITIES);
  });
});

