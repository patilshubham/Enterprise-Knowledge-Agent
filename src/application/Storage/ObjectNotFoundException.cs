namespace EnterpriseKnowledgeAgent.Application.Storage;

/// <summary>Thrown by <see cref="IObjectStorage.GetAsync"/> when the requested key does not exist.</summary>
public sealed class ObjectNotFoundException(string key) : InvalidOperationException($"Object '{key}' was not found in storage.")
{
    public string Key { get; } = key;
}
