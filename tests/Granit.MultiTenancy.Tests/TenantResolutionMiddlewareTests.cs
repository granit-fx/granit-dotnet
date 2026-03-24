// =============================================================================
// TenantResolutionMiddlewareTests - Unit tests for the tenant resolution middleware
// =============================================================================

using Granit.MultiTenancy;
using Granit.MultiTenancy.Middleware;
using Granit.MultiTenancy.Options;
using Granit.MultiTenancy.Pipeline;
using Granit.MultiTenancy.Resolvers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Tests;

public sealed class TenantResolutionMiddlewareTests
{
    private static TenantResolutionMiddleware CreateMiddleware(
        ICurrentTenant currentTenant,
        TenantResolverPipeline pipeline,
        bool isEnabled = true)
    {
        IOptions<MultiTenancyOptions> options = Microsoft.Extensions.Options.Options.Create(new MultiTenancyOptions
        {
            IsEnabled = isEnabled
        });
        return new TenantResolutionMiddleware(currentTenant, pipeline, options);
    }

    private static TenantResolverPipeline PipelineReturning(TenantInfo? tenant)
    {
        ITenantResolver resolver = Substitute.For<ITenantResolver>();
        resolver.Order.Returns(100);
        resolver.ResolveAsync(Arg.Any<HttpContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(tenant));
        return new TenantResolverPipeline([resolver]);
    }

    [Fact]
    public async Task Disabled_Skips_Resolution_And_Calls_Next()
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        TenantResolverPipeline pipeline = PipelineReturning(new TenantInfo(Guid.NewGuid()));
        TenantResolutionMiddleware middleware = CreateMiddleware(currentTenant, pipeline, isEnabled: false);
        bool nextCalled = false;
        DefaultHttpContext context = new();

        await middleware.InvokeAsync(context, _ => { nextCalled = true; return Task.CompletedTask; });

        nextCalled.ShouldBeTrue();
        currentTenant.DidNotReceive().Change(Arg.Any<Guid?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task TenantResolved_Calls_Change_And_Next()
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        IDisposable scope = Substitute.For<IDisposable>();
        var tenantId = Guid.NewGuid();
        currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(scope);

        TenantResolverPipeline pipeline = PipelineReturning(new TenantInfo(tenantId, "Acme"));
        TenantResolutionMiddleware middleware = CreateMiddleware(currentTenant, pipeline);
        bool nextCalled = false;
        DefaultHttpContext context = new();

        await middleware.InvokeAsync(context, _ => { nextCalled = true; return Task.CompletedTask; });

        nextCalled.ShouldBeTrue();
        currentTenant.Received(1).Change(tenantId, "Acme");
    }

    [Fact]
    public async Task TenantResolved_Disposes_Scope_After_Next()
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        IDisposable scope = Substitute.For<IDisposable>();
        currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(scope);

        TenantResolverPipeline pipeline = PipelineReturning(new TenantInfo(Guid.NewGuid()));
        TenantResolutionMiddleware middleware = CreateMiddleware(currentTenant, pipeline);
        DefaultHttpContext context = new();

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        scope.Received(1).Dispose();
    }

    [Fact]
    public async Task NoTenantResolved_DoesNotCall_Change()
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        TenantResolverPipeline pipeline = PipelineReturning(null);
        TenantResolutionMiddleware middleware = CreateMiddleware(currentTenant, pipeline);
        bool nextCalled = false;
        DefaultHttpContext context = new();

        await middleware.InvokeAsync(context, _ => { nextCalled = true; return Task.CompletedTask; });

        nextCalled.ShouldBeTrue();
        currentTenant.DidNotReceive().Change(Arg.Any<Guid?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task NoTenantResolved_Still_Calls_Next()
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        TenantResolverPipeline pipeline = PipelineReturning(null);
        TenantResolutionMiddleware middleware = CreateMiddleware(currentTenant, pipeline);
        bool nextCalled = false;
        DefaultHttpContext context = new();

        await middleware.InvokeAsync(context, _ => { nextCalled = true; return Task.CompletedTask; });

        nextCalled.ShouldBeTrue();
    }
}
