import '@testing-library/jest-dom/vitest';

// jsdom doesn't implement ResizeObserver; cmdk (TASK-UX-05 command palette)
// uses it to measure list height. A minimal no-op stub is enough for tests.
class ResizeObserverStub {
  observe(): void {}
  unobserve(): void {}
  disconnect(): void {}
}

globalThis.ResizeObserver ??= ResizeObserverStub;

// jsdom also doesn't implement scrollIntoView, which cmdk calls when moving
// the highlighted item into view.
Element.prototype.scrollIntoView ??= () => {};
