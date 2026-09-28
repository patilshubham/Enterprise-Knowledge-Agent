using EnterpriseKnowledgeAgent.Api.Correlation;
using EnterpriseKnowledgeAgent.Shared.Correlation;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace EnterpriseKnowledgeAgent.Api.Tests.Correlation;

/// <summary>
/// TASK-OBS-01 (ASDD §23.1): the first hop of the correlation chain - reads
/// an inbound header if present, otherwise mints a new id, always echoes it
/// back and publishes it to <see cref="CorrelationContext"/> for the
/// duration of the request.
/// </summary>
public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_without_inbound_header_generates_a_new_id_and_echoes_it()
    {
        CorrelationId? observed = null;
        var middleware = new CorrelationIdMiddleware(context =>
        {
            observed = CorrelationContext.CorrelationId;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        observed.Should().NotBeNull();
        context.Response.Headers[CorrelationContext.HeaderName].ToString().Should().Be(observed!.Value.ToString());
    }

    [Fact]
    public async Task InvokeAsync_with_valid_inbound_header_reuses_it_verbatim()
    {
        var suppliedId = Guid.NewGuid();
        CorrelationId? observed = null;
        var middleware = new CorrelationIdMiddleware(context =>
        {
            observed = CorrelationContext.CorrelationId;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationContext.HeaderName] = suppliedId.ToString();

        await middleware.InvokeAsync(context);

        observed.Should().Be(CorrelationId.From(suppliedId));
        context.Response.Headers[CorrelationContext.HeaderName].ToString().Should().Be(suppliedId.ToString());
    }

    [Fact]
    public async Task InvokeAsync_with_malformed_inbound_header_falls_back_to_a_new_id()
    {
        CorrelationId? observed = null;
        var middleware = new CorrelationIdMiddleware(context =>
        {
            observed = CorrelationContext.CorrelationId;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationContext.HeaderName] = "not-a-guid";

        await middleware.InvokeAsync(context);

        observed.Should().NotBeNull();
        observed!.Value.ToString().Should().NotBe("not-a-guid");
    }
}
