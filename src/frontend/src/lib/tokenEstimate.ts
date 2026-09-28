/**
 * TASK-AOI-04/TASK-AGT-04: client-side mirror of `HeuristicTokenEstimator`
 * (src/infrastructure/Tokenization/HeuristicTokenEstimator.cs) - the same
 * conservative ~4-characters-per-token heuristic, used so the prompt editor
 * can show a live token estimate on every keystroke without a network
 * round-trip. This is an *estimate for display only*; the server is always
 * the authority for any budget/overflow decision (module 20).
 */
export function estimateTokenCount(text: string): number {
  if (!text) {
    return 0;
  }
  return Math.max(1, Math.ceil(text.length / 4));
}
