namespace EnterpriseKnowledgeAgent.Application.Knowledge;

/// <summary>TASK-REP-01: thrown when a single-file operation (replacement) fails <see cref="FileValidationService"/> validation. The Api layer maps this to <c>400</c>.</summary>
public sealed class FileValidationFailedException(FileRejectionReason reason)
    : Exception($"File failed validation: {reason}.")
{
    public FileRejectionReason Reason { get; } = reason;
}
