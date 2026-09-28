namespace EnterpriseKnowledgeAgent.Application.Storage;

/// <summary>
/// TASK-S3-02: thrown when a caller's authorized application does not match
/// the <c>applicationId</c> segment embedded in an object key - i.e. a
/// manipulated/forged key attempting cross-application access (ASDD §11.2,
/// FR-SEC-003). Never trust a key supplied by a client; this guard applies
/// even to server-held keys that are re-validated before every read.
/// </summary>
public sealed class CrossApplicationObjectAccessException(string key, Guid authorizedApplicationId)
    : InvalidOperationException($"Object key '{key}' does not belong to application '{authorizedApplicationId}'.")
{
    public string Key { get; } = key;

    public Guid AuthorizedApplicationId { get; } = authorizedApplicationId;
}
