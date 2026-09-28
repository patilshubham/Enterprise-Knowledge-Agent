namespace EnterpriseKnowledgeAgent.Application.Knowledge;

/// <summary>TASK-KNW-02: configurable validation limits (FR-KNW-003).</summary>
public sealed class KnowledgeFileOptions
{
    public const string SectionName = "KnowledgeFiles";

    /// <summary>Maximum accepted upload size in bytes. Defaults to 10 MB.</summary>
    public long MaxFileSizeBytes { get; init; } = 10 * 1024 * 1024;
}
