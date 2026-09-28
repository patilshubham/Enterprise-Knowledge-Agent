namespace EnterpriseKnowledgeAgent.Application.Processing;

/// <summary>TASK-PRC-03: normalizes raw decoded text before parsing so downstream character offsets stay stable (line endings, BOM).</summary>
public static class TextNormalizer
{
    public static string Normalize(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        var text = raw.TrimStart('\uFEFF');
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        return text;
    }
}
