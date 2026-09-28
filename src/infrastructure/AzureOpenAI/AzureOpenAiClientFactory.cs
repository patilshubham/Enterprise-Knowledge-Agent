using System.ClientModel.Primitives;
using Azure.AI.OpenAI;
using Azure.Identity;
using EnterpriseKnowledgeAgent.Application.AzureOpenAI;
using Microsoft.Extensions.Options;

namespace EnterpriseKnowledgeAgent.Infrastructure.AzureOpenAI;

/// <summary>
/// TASK-AOI-02: builds the single shared <see cref="AzureOpenAIClient"/> for
/// the process, choosing between Entra (<see cref="DefaultAzureCredential"/>)
/// and API-key auth per <see cref="AoiOptions.AuthMode"/>. The API key (when
/// used) is only ever read here to construct the credential - never logged,
/// never exposed via any DTO/endpoint.
/// </summary>
public sealed class AzureOpenAiClientFactory(IOptions<AoiOptions> options)
{
    private readonly Lazy<AzureOpenAIClient> _client = new(() => Build(options.Value));

    public AzureOpenAIClient Client => _client.Value;

    private static AzureOpenAIClient Build(AoiOptions options)
    {
        var endpoint = new Uri(options.Endpoint);
        var clientOptions = new AzureOpenAIClientOptions();
        if (!options.ChatDeploymentSupportsMaxTokens)
        {
            // See MaxTokensCompatibilityPolicy for why this is needed instead
            // of the SDK's own SetNewMaxCompletionTokensPropertyEnabled().
            clientOptions.AddPolicy(new MaxTokensCompatibilityPolicy(options.ChatDeployment), PipelinePosition.PerCall);
        }

        return options.AuthMode switch
        {
            AoiAuthMode.ApiKey => new AzureOpenAIClient(endpoint, new Azure.AzureKeyCredential(options.ApiKey!), clientOptions),
            _ => new AzureOpenAIClient(endpoint, new DefaultAzureCredential(), clientOptions),
        };
    }
}

