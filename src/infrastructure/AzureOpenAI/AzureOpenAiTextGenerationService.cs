using System.ClientModel;
using System.Runtime.CompilerServices;
using EnterpriseKnowledgeAgent.Application.AzureOpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using AppChatMessage = EnterpriseKnowledgeAgent.Application.AzureOpenAI.ChatMessage;

namespace EnterpriseKnowledgeAgent.Infrastructure.AzureOpenAI;

/// <summary>TASK-AOI-03: Azure OpenAI-backed <see cref="ITextGenerationService"/> (used by generation modules, module 20+).</summary>
public sealed class AzureOpenAiTextGenerationService(AzureOpenAiClientFactory clientFactory, IOptions<AoiOptions> options, ILogger<AzureOpenAiTextGenerationService> logger) : ITextGenerationService
{
    /// <summary>TASK-GEN-04: schema for the opportunistic structured-output request - a single required "markdown" string property, matching the shape <see cref="EnterpriseKnowledgeAgent.Application.Generation.GenerationService"/> parses.</summary>
    private static readonly BinaryData MarkdownResponseSchema = BinaryData.FromString(
        """
        {
          "type": "object",
          "properties": {
            "markdown": { "type": "string" }
          },
          "required": ["markdown"],
          "additionalProperties": false
        }
        """);

    public async Task<TextGenerationResult> GenerateAsync(IReadOnlyList<AppChatMessage> messages, string deployment, GenerationParameters? parameters = null, CancellationToken cancellationToken = default)
    {
        var chatClient = clientFactory.Client.GetChatClient(deployment);
        var sdkMessages = messages.Select(ToSdkMessage).ToList();
        var completionOptions = ToCompletionOptions(parameters);

        var completion = await AzureOpenAiRetryPolicy.ExecuteAsync(
            ct => chatClient.CompleteChatAsync(sdkMessages, completionOptions, ct),
            options.Value,
            "chat",
            cancellationToken).ConfigureAwait(false);

        var text = string.Concat(completion.Value.Content.Select(part => part.Text));
        return new TextGenerationResult(text, completion.Value.FinishReason.ToString());
    }

    public async IAsyncEnumerable<string> GenerateStreamingAsync(
        IReadOnlyList<AppChatMessage> messages,
        string deployment,
        GenerationParameters? parameters = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var chatClient = clientFactory.Client.GetChatClient(deployment);
        var sdkMessages = messages.Select(ToSdkMessage).ToList();
        var completionOptions = ToCompletionOptions(parameters);

        // The updates are produced lazily as the network response streams, so
        // (unlike GenerateAsync) this path cannot use the bounded-retry loop;
        // failures are mapped to the shared AzureOpenAiException instead.
        var stream = chatClient.CompleteChatStreamingAsync(sdkMessages, completionOptions, cancellationToken);
        await using var enumerator = stream.GetAsyncEnumerator(cancellationToken);

        while (true)
        {
            StreamingChatCompletionUpdate update;
            try
            {
                if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    break;
                }

                update = enumerator.Current;
            }
            catch (ClientResultException ex)
            {
                throw AzureOpenAiRetryPolicy.MapClientException(ex);
            }

            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                {
                    yield return part.Text;
                }
            }
        }
    }

    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var chatClient = clientFactory.Client.GetChatClient(options.Value.ChatDeployment);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(options.Value.RequestTimeoutSeconds));
            // Reasoning-style deployments (e.g. gpt-5.5) spend some of the
            // output budget on hidden reasoning tokens before any visible
            // text - a budget of 1 makes Azure return a hard 400 ("Could not
            // finish the message because max_tokens ... was reached")
            // instead of a normal completion, which made this health check
            // permanently report "unavailable" for those deployments. 16 is
            // enough headroom for a one-word reply on every deployment seen
            // so far while still being a cheap probe.
            var healthCheckOptions = new ChatCompletionOptions { MaxOutputTokenCount = 16 };
            await chatClient.CompleteChatAsync(
                [new UserChatMessage("ping")],
                healthCheckOptions,
                timeoutCts.Token).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            logger.ChatHealthCheckFailed(ex, options.Value.ChatDeployment);
            return false;
        }
    }

    /// <summary>TASK-GEN-01: maps agent-configured parameters to the SDK's options - <see cref="GenerationParameters.TopP"/> is only set when non-null (the caller has already zeroed it out for deployments that don't support it). <see cref="GenerationParameters.Temperature"/> is only set when <see cref="AoiOptions.ChatDeploymentSupportsTemperature"/> is true - reasoning-style deployments (e.g. gpt-5.5) reject any non-default temperature with a hard 400, so those are left at the model's own default instead.</summary>
    private ChatCompletionOptions? ToCompletionOptions(GenerationParameters? parameters)
    {
        if (parameters is null)
        {
            return null;
        }

        var completionOptions = new ChatCompletionOptions
        {
            MaxOutputTokenCount = parameters.MaximumOutputTokens,
        };

        if (options.Value.ChatDeploymentSupportsTemperature)
        {
            completionOptions.Temperature = (float)parameters.Temperature;
        }

        if (parameters.TopP is { } topP)
        {
            completionOptions.TopP = (float)topP;
        }

        if (parameters.RequestStructuredOutput)
        {
            completionOptions.ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                "markdown_response", MarkdownResponseSchema, jsonSchemaIsStrict: true);
        }

        return completionOptions;
    }

    private static OpenAI.Chat.ChatMessage ToSdkMessage(AppChatMessage message) => message.Role switch
    {
        "system" => new SystemChatMessage(message.Content),
        "assistant" => new AssistantChatMessage(message.Content),
        _ => new UserChatMessage(message.Content),
    };
}
