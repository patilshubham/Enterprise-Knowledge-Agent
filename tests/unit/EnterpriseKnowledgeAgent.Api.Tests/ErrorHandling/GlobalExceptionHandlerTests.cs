using System.Text.Json;
using EnterpriseKnowledgeAgent.Api.ErrorHandling;
using EnterpriseKnowledgeAgent.Application.AzureOpenAI;
using EnterpriseKnowledgeAgent.Application.Execution;
using EnterpriseKnowledgeAgent.Shared.Correlation;
using EnterpriseKnowledgeAgent.Shared.Errors;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EnterpriseKnowledgeAgent.Api.Tests.ErrorHandling;

/// <summary>TASK-ERR-01 (ASDD §22): the global Problem Details safety-net exception handler.</summary>
public sealed class GlobalExceptionHandlerTests
{
    private const string TestCorrelationId = "11111111-1111-1111-1111-111111111111";

    private static (GlobalExceptionHandler Handler, DefaultHttpContext Context) CreateSubject()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();
        var provider = services.BuildServiceProvider();

        var handler = new GlobalExceptionHandler(
            provider.GetRequiredService<IProblemDetailsService>(),
            NullLogger<GlobalExceptionHandler>.Instance);

        // TASK-OBS-01: the real request pipeline's CorrelationIdMiddleware
        // always sets this ambient value before the exception handler runs;
        // simulate that here rather than relying on HttpContext.TraceIdentifier.
        CorrelationContext.CorrelationId = CorrelationId.From(Guid.Parse(TestCorrelationId));

        var context = new DefaultHttpContext { RequestServices = provider };
        context.Response.Body = new MemoryStream();

        return (handler, context);
    }

    private static async Task<JsonElement> ReadProblemJsonAsync(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    [Fact]
    public async Task TryHandleAsync_unclassified_exception_maps_to_internal_failure_and_hides_the_raw_message()
    {
        var (handler, context) = CreateSubject();

        var handled = await handler.TryHandleAsync(context, new InvalidOperationException("raw internal secret detail"), CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(500);

        var json = await ReadProblemJsonAsync(context);
        json.GetProperty("title").GetString().Should().Be("Unexpected error");
        json.GetProperty("detail").GetString().Should().NotContain("raw internal secret detail");
        json.GetProperty("errorCategory").GetString().Should().Be(nameof(ErrorCategory.InternalFailure));
        json.GetProperty("correlationId").GetString().Should().Be(TestCorrelationId);
        json.GetProperty("retryable").GetBoolean().Should().BeFalse();
        json.GetProperty("recommendedAction").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TryHandleAsync_categorized_AzureOpenAiException_uses_its_category()
    {
        var (handler, context) = CreateSubject();

        var handled = await handler.TryHandleAsync(
            context, new AzureOpenAiException(ErrorCategory.Throttled, "rate limited by AOAI"), CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(429);

        var json = await ReadProblemJsonAsync(context);
        json.GetProperty("errorCategory").GetString().Should().Be(nameof(ErrorCategory.Throttled));
        json.GetProperty("retryable").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task TryHandleAsync_categorized_AgentRunExecutionException_uses_its_category()
    {
        var (handler, context) = CreateSubject();

        var handled = await handler.TryHandleAsync(
            context, new AgentRunExecutionException(ErrorCategory.ContextOverflow, "too much context"), CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(400);

        var json = await ReadProblemJsonAsync(context);
        json.GetProperty("errorCategory").GetString().Should().Be(nameof(ErrorCategory.ContextOverflow));
    }

    [Fact]
    public async Task TryHandleAsync_ArgumentException_maps_to_validation()
    {
        var (handler, context) = CreateSubject();

        var handled = await handler.TryHandleAsync(context, new ArgumentException("bad input"), CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(400);

        var json = await ReadProblemJsonAsync(context);
        json.GetProperty("errorCategory").GetString().Should().Be(nameof(ErrorCategory.Validation));
    }
}
