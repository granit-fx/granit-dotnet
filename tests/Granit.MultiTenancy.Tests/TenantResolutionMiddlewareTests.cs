// =============================================================================
// TenantResolutionMiddlewareTests - Unit tests for the tenant resolution middleware
// =============================================================================

using System.Diagnostics.Metrics;
using Granit.MultiTenancy;
using Granit.MultiTenancy.Diagnostics;
using Granit.MultiTenancy.Middleware;
using Granit.MultiTenancy.Options;
using Granit.MultiTenancy.Pipeline;
using Granit.MultiTenancy.Resolvers;
using Granit.MultiTenancy.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Tests;

public sealed class TenantResolutionMiddlewareTests
{
    private static MultiTenancyMetrics CreateMetrics()
    {
        IMeterFactory meterFactory = Substitute.For<IMeterFactory>();
        meterFactory.Create(Arg.Any<MeterOptions>()).Returns(callInfo => new Meter(callInfo.Arg<MeterOptions>().Name));
        return new MultiTenancyMetrics(meterFactory);
    }

    private static TenantResolutionMiddleware CreateMiddleware(
        ICurrentTenant currentTenant,
        TenantResolverPipeline pipeline,
        bool isEnabled = true,
        TenantHeaderTrustMode headerTrustMode = TenantHeaderTrustMode.Unrestricted,
        bool validateTenantExistence = false)
    {
        IOptions<MultiTenancyOptions> options = Microsoft.Extensions.Options.Options.Create(new MultiTenancyOptions
        {
            IsEnabled = isEnabled,
            HeaderTrustMode = headerTrustMode,
            ValidateTenantExistence = validateTenantExistence,
        });
        ITenantReader tenantReader = Substitute.For<ITenantReader>();
        tenantReader.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        return new TenantResolutionMiddleware(
            currentTenant,
            pipeline,
            tenantReader,
            CreateMetrics(),
            options,
            NullLogger<TenantResolutionMiddleware>.Instance);
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

    // ── Phantom tenant validation (VULN-201) ──────────────────────────

    [Fact]
    public async Task ValidateTenantExistence_Enabled_ExistingTenant_Passes()
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        IDisposable scope = Substitute.For<IDisposable>();
        currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(scope);
        var tenantId = Guid.NewGuid();
        TenantResolverPipeline pipeline = PipelineReturning(new TenantInfo(tenantId));
        TenantResolutionMiddleware middleware = CreateMiddleware(
            currentTenant, pipeline, validateTenantExistence: true);
        DefaultHttpContext context = new();

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        currentTenant.Received(1).Change(tenantId, Arg.Any<string?>());
    }

    [Fact]
    public async Task ValidateTenantExistence_Enabled_PhantomTenant_Returns403()
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        var phantomId = Guid.NewGuid();
        TenantResolverPipeline pipeline = PipelineReturning(new TenantInfo(phantomId));
        TenantResolutionMiddleware middleware = CreateMiddlewareWithPhantomRejection(
            currentTenant, pipeline);
        DefaultHttpContext context = new();

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        currentTenant.DidNotReceive().Change(Arg.Any<Guid?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task ValidateTenantExistence_Disabled_PhantomTenant_Passes()
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        IDisposable scope = Substitute.For<IDisposable>();
        currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(scope);
        var phantomId = Guid.NewGuid();
        TenantResolverPipeline pipeline = PipelineReturning(new TenantInfo(phantomId));
        TenantResolutionMiddleware middleware = CreateMiddleware(
            currentTenant, pipeline, validateTenantExistence: false);
        DefaultHttpContext context = new();

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        currentTenant.Received(1).Change(phantomId, Arg.Any<string?>());
    }

    private static TenantResolutionMiddleware CreateMiddlewareWithPhantomRejection(
        ICurrentTenant currentTenant,
        TenantResolverPipeline pipeline)
    {
        IOptions<MultiTenancyOptions> options = Microsoft.Extensions.Options.Options.Create(new MultiTenancyOptions
        {
            ValidateTenantExistence = true,
        });
        ITenantReader tenantReader = Substitute.For<ITenantReader>();
        tenantReader.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false)); // Tenant does NOT exist
        return new TenantResolutionMiddleware(
            currentTenant,
            pipeline,
            tenantReader,
            CreateMetrics(),
            options,
            NullLogger<TenantResolutionMiddleware>.Instance);
    }
}
