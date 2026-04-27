using AuditLogService.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace AuditLogService.Api.Tests.Middleware;

public sealed class CorrelationIdMiddlewareTests
{
    private const string HeaderName = "X-Correlation-ID";

    private static CorrelationIdMiddleware Build(RequestDelegate next) =>
        new(next, NullLogger<CorrelationIdMiddleware>.Instance);

    [Fact]
    public async Task GeneratesCorrelationId_WhenHeaderMissing()
    {
        var ctx = new DefaultHttpContext();
        var middleware = Build(_ => Task.CompletedTask);

        await middleware.InvokeAsync(ctx);

        var stored = ctx.Items["CorrelationId"] as string;
        stored.ShouldNotBeNullOrWhiteSpace();
        Guid.TryParse(stored, out _).ShouldBeTrue();
        ctx.Response.Headers[HeaderName].ToString().ShouldBe(stored);
    }

    [Fact]
    public async Task EchoesCorrelationId_WhenHeaderProvided()
    {
        const string supplied = "abc-123-correlation";
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers[HeaderName] = supplied;
        var middleware = Build(_ => Task.CompletedTask);

        await middleware.InvokeAsync(ctx);

        ctx.Items["CorrelationId"].ShouldBe(supplied);
        ctx.Response.Headers[HeaderName].ToString().ShouldBe(supplied);
    }

    [Fact]
    public async Task CallsNext()
    {
        var ctx = new DefaultHttpContext();
        var called = false;
        var middleware = Build(_ => { called = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(ctx);

        called.ShouldBeTrue();
    }

    [Fact]
    public async Task CorrelationIdAvailable_DuringPipelineExecution()
    {
        var ctx = new DefaultHttpContext();
        string? observed = null;
        var middleware = Build(c =>
        {
            observed = c.Items["CorrelationId"] as string;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(ctx);

        observed.ShouldNotBeNullOrWhiteSpace();
    }
}
