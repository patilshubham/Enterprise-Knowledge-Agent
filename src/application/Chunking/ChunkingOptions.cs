namespace EnterpriseKnowledgeAgent.Application.Chunking;

/// <summary>TASK-CHK-01: token target/max/overlap config (ASDD §13.1) - starting defaults, tunable per TASK-CHK-03 findings.</summary>
public sealed class ChunkingOptions
{
    public const string SectionName = "Chunking";

    public int TargetTokens { get; init; } = 700;

    public int MaxTokens { get; init; } = 1100;

    public int OverlapTokens { get; init; } = 100;
}
