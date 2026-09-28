namespace EnterpriseKnowledgeAgent.Application.Knowledge;

/// <summary>TASK-KNW-02: per-file reasons a candidate upload was rejected (FR-KNW-003).</summary>
public enum FileRejectionReason
{
    UnsupportedExtension,
    TooLarge,
    Empty,
    UnsupportedEncoding,
    BinaryContent,
    UnsafeFileName,
}

/// <summary>TASK-KNW-02: result of validating one candidate upload before storage.</summary>
public sealed record FileValidationResult(bool IsValid, FileRejectionReason? Reason, string? SanitizedFileName, string? DetectedEncodingWebName)
{
    public static FileValidationResult Valid(string sanitizedFileName, string detectedEncodingWebName) =>
        new(true, null, sanitizedFileName, detectedEncodingWebName);

    public static FileValidationResult Invalid(FileRejectionReason reason) => new(false, reason, null, null);
}
