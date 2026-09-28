using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json.Nodes;

namespace EnterpriseKnowledgeAgent.Infrastructure.AzureOpenAI;

/// <summary>
/// TASK-AOI-05: works around an Azure.AI.OpenAI 2.1.0 SDK gap for reasoning-style chat
/// deployments (e.g. gpt-5.5). The installed SDK's internal <c>AzureChatClient</c> always
/// serializes <see cref="OpenAI.Chat.ChatCompletionOptions.MaxOutputTokenCount"/> as the
/// legacy <c>max_tokens</c> request field unless the request options are first marked via
/// the public <c>ChatCompletionOptions.SetNewMaxCompletionTokensPropertyEnabled()</c>
/// extension - but that helper itself throws <see cref="ArgumentNullException"/> on a
/// freshly-constructed options instance in this SDK version (verified directly against
/// the installed 2.1.0 package: its raw-additional-data dictionary is null until the
/// object has been through a JSON round-trip, and the helper never null-checks it, unlike
/// its sibling <c>AddDataSource</c>). Reasoning deployments reject <c>max_tokens</c>
/// outright with a hard 400 and require <c>max_completion_tokens</c> instead, so this
/// policy rewrites the outgoing JSON body at the wire level for chat completions requests
/// to the configured reasoning deployment, sidestepping the broken helper entirely.
/// </summary>
internal sealed class MaxTokensCompatibilityPolicy(string deploymentName) : PipelinePolicy
{
    private readonly string _deploymentSegment = $"/deployments/{deploymentName}/chat/completions";

    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        if (IsTargetRequest(message))
        {
            using var buffer = new MemoryStream();
            message.Request.Content!.WriteTo(buffer, default);
            RewriteMaxTokens(message, buffer);
        }

        ProcessNext(message, pipeline, currentIndex);
    }

    public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        if (IsTargetRequest(message))
        {
            using var buffer = new MemoryStream();
            await message.Request.Content!.WriteToAsync(buffer, default).ConfigureAwait(false);
            RewriteMaxTokens(message, buffer);
        }

        await ProcessNextAsync(message, pipeline, currentIndex).ConfigureAwait(false);
    }

    private bool IsTargetRequest(PipelineMessage message)
        => message.Request.Content is not null
            && message.Request.Uri?.AbsolutePath.Contains(_deploymentSegment, StringComparison.OrdinalIgnoreCase) == true;

    private static void RewriteMaxTokens(PipelineMessage message, MemoryStream buffer)
    {
        buffer.Position = 0;
        if (JsonNode.Parse(buffer) is JsonObject body
            && body.TryGetPropertyValue("max_tokens", out JsonNode? maxTokensValue))
        {
            body.Remove("max_tokens");
            body["max_completion_tokens"] = maxTokensValue;
            message.Request.Content = BinaryContent.Create(BinaryData.FromString(body.ToJsonString()));
        }
    }
}
