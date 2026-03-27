// =============================================================================
// TenantResolverPipelineTests - Unit tests for the resolver pipeline
// =============================================================================

using Granit.MultiTenancy.Pipeline;
using Granit.MultiTenancy.Resolvers;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Tests;

public sealed class TenantResolverPipelineTests
{
    private static ITenantResolver MockResolver(int order, TenantInfo? result)
    {
        ITenantResolver resolver = Substitute.For<ITenantResolver>();
        resolver.Order.Returns(order);
        resolver.ResolveAsync(Arg.Any<HttpContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));
        return resolver;
    }

    [Fact]
    public async Task ResolveAsync_NoResolvers_ReturnsNullTenant()
    {
        TenantResolverPipeline pipeline = new([]);
        DefaultHttpContext context = new();

        TenantResolutionResult result = await pipeline.ResolveAsync(context, TestContext.Current.CancellationToken);

        result.Tenant.ShouldBeNull();
        result.ResolverType.ShouldBe("none");
    }

    [Fact]
    public async Task ResolveAsync_SingleResolver_ReturnsTenant()
    {
        var tenantId = Guid.NewGuid();
        ITenantResolver resolver = MockResolver(100, new TenantInfo(tenantId, "Acme"));
        TenantResolverPipeline pipeline = new([resolver]);
        DefaultHttpContext context = new();

        TenantResolutionResult result = await pipeline.ResolveAsync(context, TestContext.Current.CancellationToken);

        result.Tenant.ShouldNotBeNull();
        result.Tenant!.Id.ShouldBe(tenantId);
        result.Tenant.Name.ShouldBe("Acme");
        result.ResolverType.ShouldNotBe("none");
    }

    [Fact]
    public async Task ResolveAsync_SingleResolver_ReturnsNullTenant_WhenResolverReturnsNull()
    {
        ITenantResolver resolver = MockResolver(100, null);
        TenantResolverPipeline pipeline = new([resolver]);
        DefaultHttpContext context = new();

        TenantResolutionResult result = await pipeline.ResolveAsync(context, TestContext.Current.CancellationToken);

        result.Tenant.ShouldBeNull();
        result.ResolverType.ShouldBe("none");
    }

    [Fact]
    public async Task ResolveAsync_FirstMatchWins_SkipsSubsequentResolvers()
    {
        var tenantId = Guid.NewGuid();
        ITenantResolver first = MockResolver(100, new TenantInfo(tenantId, "First"));
        ITenantResolver second = MockResolver(200, new TenantInfo(Guid.NewGuid(), "Second"));
        TenantResolverPipeline pipeline = new([first, second]);
        DefaultHttpContext context = new();

        TenantResolutionResult result = await pipeline.ResolveAsync(context, TestContext.Current.CancellationToken);

        result.Tenant.ShouldNotBeNull();
        result.Tenant!.Id.ShouldBe(tenantId);
        result.Tenant.Name.ShouldBe("First");
        await second.DidNotReceive().ResolveAsync(Arg.Any<HttpContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_FallsThrough_WhenFirstResolverReturnsNull()
    {
        var tenantId = Guid.NewGuid();
        ITenantResolver first = MockResolver(100, null);
        ITenantResolver second = MockResolver(200, new TenantInfo(tenantId, "Fallback"));
        TenantResolverPipeline pipeline = new([first, second]);
        DefaultHttpContext context = new();

        TenantResolutionResult result = await pipeline.ResolveAsync(context, TestContext.Current.CancellationToken);

        result.Tenant.ShouldNotBeNull();
        result.Tenant!.Id.ShouldBe(tenantId);
        result.Tenant.Name.ShouldBe("Fallback");
    }

    [Fact]
    public async Task ResolveAsync_AllResolversReturnNull_ReturnsNullTenant()
    {
        ITenantResolver first = MockResolver(100, null);
        ITenantResolver second = MockResolver(200, null);
        ITenantResolver third = MockResolver(300, null);
        TenantResolverPipeline pipeline = new([first, second, third]);
        DefaultHttpContext context = new();

        TenantResolutionResult result = await pipeline.ResolveAsync(context, TestContext.Current.CancellationToken);

        result.Tenant.ShouldBeNull();
        result.ResolverType.ShouldBe("none");
    }

    [Fact]
    public async Task ResolveAsync_ResolversAreOrderedByOrder_RegardlessOfRegistration()
    {
        var tenantId = Guid.NewGuid();
        // Register in reverse order — pipeline should sort by Order ascending
        ITenantResolver highOrder = MockResolver(300, new TenantInfo(Guid.NewGuid(), "High"));
        ITenantResolver lowOrder = MockResolver(50, new TenantInfo(tenantId, "Low"));
        ITenantResolver midOrder = MockResolver(150, new TenantInfo(Guid.NewGuid(), "Mid"));
        TenantResolverPipeline pipeline = new([highOrder, lowOrder, midOrder]);
        DefaultHttpContext context = new();

        TenantResolutionResult result = await pipeline.ResolveAsync(context, TestContext.Current.CancellationToken);

        result.Tenant.ShouldNotBeNull();
        result.Tenant!.Id.ShouldBe(tenantId);
        result.Tenant.Name.ShouldBe("Low");
    }

    [Fact]
    public async Task ResolveAsync_PassesHttpContext_ToResolvers()
    {
        ITenantResolver resolver = MockResolver(100, null);
        TenantResolverPipeline pipeline = new([resolver]);
        DefaultHttpContext context = new();

        await pipeline.ResolveAsync(context, TestContext.Current.CancellationToken);

        await resolver.Received(1).ResolveAsync(context, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_PassesCancellationToken_ToResolvers()
    {
        ITenantResolver resolver = MockResolver(100, null);
        TenantResolverPipeline pipeline = new([resolver]);
        DefaultHttpContext context = new();
        using CancellationTokenSource cts = new();
        CancellationToken token = cts.Token;

        await pipeline.ResolveAsync(context, token);

        await resolver.Received(1).ResolveAsync(Arg.Any<HttpContext>(), token);
    }
}
