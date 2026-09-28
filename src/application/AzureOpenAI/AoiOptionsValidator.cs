using Microsoft.Extensions.Options;

namespace EnterpriseKnowledgeAgent.Application.AzureOpenAI;

/// <summary>
/// TASK-AOI-01: fail-fast validation of <see cref="AoiOptions"/> at startup
/// (the first <see cref="IValidateOptions{TOptions}"/> in this codebase -
/// registered via <c>ValidateOnStart()</c> so a misconfiguration surfaces
/// immediately rather than on first use).
/// </summary>
public sealed class AoiOptionsValidator : IValidateOptions<AoiOptions>
{
    public ValidateOptionsResult Validate(string? name, AoiOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            errors.Add($"{AoiOptions.SectionName}:{nameof(AoiOptions.Endpoint)} is required.");
        }
        else if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out _))
        {
            errors.Add($"{AoiOptions.SectionName}:{nameof(AoiOptions.Endpoint)} must be an absolute URI.");
        }

        if (!Enum.IsDefined(options.AuthMode))
        {
            errors.Add($"{AoiOptions.SectionName}:{nameof(AoiOptions.AuthMode)} must be one of: {string.Join(", ", Enum.GetNames<AoiAuthMode>())}.");
        }
        else if (options.AuthMode == AoiAuthMode.ApiKey && string.IsNullOrWhiteSpace(options.ApiKey))
        {
            errors.Add($"{AoiOptions.SectionName}:{nameof(AoiOptions.ApiKey)} is required when {nameof(AoiOptions.AuthMode)} is {nameof(AoiAuthMode.ApiKey)}.");
        }

        if (string.IsNullOrWhiteSpace(options.ChatDeployment))
        {
            errors.Add($"{AoiOptions.SectionName}:{nameof(AoiOptions.ChatDeployment)} is required.");
        }

        if (string.IsNullOrWhiteSpace(options.EmbeddingDeployment))
        {
            errors.Add($"{AoiOptions.SectionName}:{nameof(AoiOptions.EmbeddingDeployment)} is required.");
        }

        if (options.EmbeddingDimensions <= 0)
        {
            errors.Add($"{AoiOptions.SectionName}:{nameof(AoiOptions.EmbeddingDimensions)} must be greater than zero.");
        }

        if (options.MaxContextTokens <= 0)
        {
            errors.Add($"{AoiOptions.SectionName}:{nameof(AoiOptions.MaxContextTokens)} must be greater than zero.");
        }

        if (options.MaxOutputTokens <= 0)
        {
            errors.Add($"{AoiOptions.SectionName}:{nameof(AoiOptions.MaxOutputTokens)} must be greater than zero.");
        }

        if (options.RequestTimeoutSeconds <= 0)
        {
            errors.Add($"{AoiOptions.SectionName}:{nameof(AoiOptions.RequestTimeoutSeconds)} must be greater than zero.");
        }

        if (options.MaxRetries < 0)
        {
            errors.Add($"{AoiOptions.SectionName}:{nameof(AoiOptions.MaxRetries)} must not be negative.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
