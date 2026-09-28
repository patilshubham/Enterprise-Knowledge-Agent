using System.Text.RegularExpressions;

namespace EnterpriseKnowledgeAgent.Application.Storage;

/// <summary>
/// TASK-S3-02: produces canonical, server-generated object keys embedding
/// the owning application id (ASDD §11.2). Clients never supply keys -
/// every read/download must additionally call <see cref="RequireApplication"/>
/// (or <see cref="TryGetApplicationId"/>) to guard against a manipulated key
/// referencing another application (FR-SEC-003, failure scenario "S3 key
/// manipulation").
/// </summary>
public static partial class ObjectKeyFactory
{
    /// <summary>Canonical source-object key: <c>applications/{applicationId}/files/{fileId}/source</c>.</summary>
    public static string Source(Guid applicationId, Guid fileId) =>
        $"applications/{applicationId}/files/{fileId}/source";

    /// <summary>Canonical replacement-object key: <c>applications/{applicationId}/files/{fileId}/replacement/{replacementId}</c> (TASK-KNW replacement flow, module 15).</summary>
    public static string Replacement(Guid applicationId, Guid fileId, Guid replacementId) =>
        $"applications/{applicationId}/files/{fileId}/replacement/{replacementId}";

    /// <summary>Attempts to parse the <c>applicationId</c> segment embedded in a canonical key produced by this factory.</summary>
    public static bool TryGetApplicationId(string key, out Guid applicationId)
    {
        var match = KeyPattern().Match(key);
        if (match.Success && Guid.TryParse(match.Groups["applicationId"].Value, out var parsed))
        {
            applicationId = parsed;
            return true;
        }

        applicationId = Guid.Empty;
        return false;
    }

    /// <summary>
    /// Throws <see cref="CrossApplicationObjectAccessException"/> unless <paramref name="key"/> is a
    /// well-formed key belonging to <paramref name="authorizedApplicationId"/>. Callers must invoke
    /// this before every storage read/delete driven by a persisted key.
    /// </summary>
    public static void RequireApplication(string key, Guid authorizedApplicationId)
    {
        if (!TryGetApplicationId(key, out var keyApplicationId) || keyApplicationId != authorizedApplicationId)
        {
            throw new CrossApplicationObjectAccessException(key, authorizedApplicationId);
        }
    }

    [GeneratedRegex(@"^applications/(?<applicationId>[0-9a-fA-F-]{36})/files/")]
    private static partial Regex KeyPattern();
}
