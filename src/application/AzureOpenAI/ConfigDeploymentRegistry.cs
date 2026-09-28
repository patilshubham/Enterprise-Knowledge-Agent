using Microsoft.Extensions.Options;

namespace EnterpriseKnowledgeAgent.Application.AzureOpenAI;

/// <summary>TASK-AOI-01: builds the deployment list directly from <see cref="AoiOptions"/> - no external call, so the registry stays available even during an AOAI outage.</summary>
public sealed class ConfigDeploymentRegistry(IOptions<AoiOptions> options) : IDeploymentRegistry
{
    public IReadOnlyList<DeploymentInfo> ListDeployments()
    {
        var aoi = options.Value;
        return
        [
            new DeploymentInfo(aoi.ChatDeployment, DeploymentPurpose.Chat, aoi.MaxContextTokens, aoi.MaxOutputTokens, EmbeddingDimensions: null, aoi.ChatDeploymentSupportsTopP, aoi.ChatDeploymentSupportsStructuredOutput, aoi.ChatDeploymentSupportsTemperature),
            new DeploymentInfo(aoi.EmbeddingDeployment, DeploymentPurpose.Embedding, MaxContextTokens: null, MaxOutputTokens: null, aoi.EmbeddingDimensions),
        ];
    }
}
