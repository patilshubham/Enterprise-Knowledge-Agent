using EnterpriseKnowledgeAgent.Application.AzureOpenAI;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Application.Tests.AzureOpenAI;

/// <summary>TASK-AOI-01: fail-fast validation of <see cref="AoiOptions"/>.</summary>
public sealed class AoiOptionsValidatorTests
{
    private static AoiOptions Options(
        string? endpoint = "https://my-resource.openai.azure.com/",
        AoiAuthMode authMode = AoiAuthMode.Entra,
        string? apiKey = null,
        string chatDeployment = "gpt-4o",
        string embeddingDeployment = "text-embedding-3-small",
        int embeddingDimensions = 1536,
        int maxContextTokens = 128_000,
        int maxOutputTokens = 4_096,
        int requestTimeoutSeconds = 60,
        int maxRetries = 3) => new()
        {
            Endpoint = endpoint ?? string.Empty,
            AuthMode = authMode,
            ApiKey = apiKey,
            ChatDeployment = chatDeployment,
            EmbeddingDeployment = embeddingDeployment,
            EmbeddingDimensions = embeddingDimensions,
            MaxContextTokens = maxContextTokens,
            MaxOutputTokens = maxOutputTokens,
            RequestTimeoutSeconds = requestTimeoutSeconds,
            MaxRetries = maxRetries,
        };

    private readonly AoiOptionsValidator validator = new();

    [Fact]
    public void Validate_succeeds_for_a_fully_populated_entra_config()
    {
        var result = validator.Validate(null, Options());

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_succeeds_for_apikey_mode_with_a_key_present()
    {
        var result = validator.Validate(null, Options(authMode: AoiAuthMode.ApiKey, apiKey: "test-key"));

        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_fails_when_endpoint_is_missing(string? endpoint)
    {
        var result = validator.Validate(null, Options(endpoint: endpoint));

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains(nameof(AoiOptions.Endpoint)));
    }

    [Fact]
    public void Validate_fails_when_endpoint_is_not_an_absolute_uri()
    {
        var result = validator.Validate(null, Options(endpoint: "not-a-uri"));

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("absolute URI"));
    }

    [Fact]
    public void Validate_fails_when_apikey_mode_has_no_key()
    {
        var result = validator.Validate(null, Options(authMode: AoiAuthMode.ApiKey, apiKey: null));

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains(nameof(AoiOptions.ApiKey)));
    }

    [Fact]
    public void Validate_fails_when_chat_deployment_is_missing()
    {
        var result = validator.Validate(null, Options(chatDeployment: ""));

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains(nameof(AoiOptions.ChatDeployment)));
    }

    [Fact]
    public void Validate_fails_when_embedding_deployment_is_missing()
    {
        var result = validator.Validate(null, Options(embeddingDeployment: ""));

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains(nameof(AoiOptions.EmbeddingDeployment)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_fails_when_embedding_dimensions_is_not_positive(int dimensions)
    {
        var result = validator.Validate(null, Options(embeddingDimensions: dimensions));

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains(nameof(AoiOptions.EmbeddingDimensions)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_fails_when_max_context_tokens_is_not_positive(int tokens)
    {
        var result = validator.Validate(null, Options(maxContextTokens: tokens));

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains(nameof(AoiOptions.MaxContextTokens)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_fails_when_max_output_tokens_is_not_positive(int tokens)
    {
        var result = validator.Validate(null, Options(maxOutputTokens: tokens));

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains(nameof(AoiOptions.MaxOutputTokens)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_fails_when_request_timeout_is_not_positive(int seconds)
    {
        var result = validator.Validate(null, Options(requestTimeoutSeconds: seconds));

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains(nameof(AoiOptions.RequestTimeoutSeconds)));
    }

    [Fact]
    public void Validate_fails_when_max_retries_is_negative()
    {
        var result = validator.Validate(null, Options(maxRetries: -1));

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains(nameof(AoiOptions.MaxRetries)));
    }

    [Fact]
    public void Validate_reports_every_violation_at_once()
    {
        var options = Options(
            endpoint: "",
            chatDeployment: "",
            embeddingDeployment: "",
            embeddingDimensions: 0,
            maxContextTokens: 0,
            maxOutputTokens: 0,
            requestTimeoutSeconds: 0,
            maxRetries: -1);

        var result = validator.Validate(null, options);

        result.Failures.Should().HaveCount(8);
    }
}

