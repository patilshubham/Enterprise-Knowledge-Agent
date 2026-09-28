namespace EnterpriseKnowledgeAgent.Shared.Knowledge;

/// <summary>
/// Internal document-processing pipeline stages (FR-PRC-002). Progress is
/// derived from completed stages plus embedding-batch counts (FR-PRC-003) -
/// never a fake continuously increasing percentage.
/// </summary>
public enum ProcessingStage
{
    Queued,
    Downloading,
    Validating,
    Parsing,
    Chunking,
    GeneratingEmbeddings,
    SavingVectors,
    Finalizing,
    Completed,
}
