// =============================================================================
// HeaderTenantResolverTests - Unit tests for the HTTP header tenant resolver
// =============================================================================

using Granit.MultiTenancy.Options;
using Granit.MultiTenancy.Resolvers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Tests;

public sealed class HeaderTenantResolverTests
{
    private static HeaderTenantResolver CreateResolver(string headerName = "X-Tenant-Id")
    {
        IOptions<MultiTenancyOptions> options = Microsoft.Extensions.Options.Options.Create(new MultiTenancyOptions
        {
            TenantIdHeaderName = headerName
        });
        return new HeaderTenantResolver(options);
    }

    private static DefaultHttpContext ContextWithHeader(string name, string value)
    {
        DefaultHttpContext context = new();
        context.Request.Headers.Append(name, value);
        return context;
    }

    [Fact]
    public void Order_Is_100()
    {
        HeaderTenantResolver resolver = CreateResolver();
        resolver.Order.ShouldBe(100);
    }

    [Fact]
    public async Task ValidHeader_Returns_TenantInfo()
    {
        HeaderTenantResolver resolver = CreateResolver();
        var tenantId = Guid.NewGuid();
        DefaultHttpContext context = ContextWithHeader("X-Tenant-Id", tenantId.ToString());

        TenantInfo? result = await resolver.ResolveAsync(context, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result!.Id.ShouldBe(tenantId);
    }

    [Fact]
    public async Task MissingHeader_Returns_Null()
    {
        HeaderTenantResolver resolver = CreateResolver();
        DefaultHttpContext context = new();

        TenantInfo? result = await resolver.ResolveAsync(context, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task EmptyHeader_Returns_Null()
    {
        HeaderTenantResolver resolver = CreateResolver();
        DefaultHttpContext context = ContextWithHeader("X-Tenant-Id", string.Empty);

        TenantInfo? result = await resolver.ResolveAsync(context, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task InvalidGuid_Returns_Null()
    {
        HeaderTenantResolver resolver = CreateResolver();
        DefaultHttpContext context = ContextWithHeader("X-Tenant-Id", "not-a-guid");

        TenantInfo? result = await resolver.ResolveAsync(context, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task CustomHeaderName_Is_Respected()
    {
        HeaderTenantResolver resolver = CreateResolver("X-Custom-Tenant");
        var tenantId = Guid.NewGuid();
        DefaultHttpContext context = ContextWithHeader("X-Custom-Tenant", tenantId.ToString());

        TenantInfo? result = await resolver.ResolveAsync(context, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result!.Id.ShouldBe(tenantId);
    }

    [Fact]
    public async Task WrongHeaderName_Returns_Null()
    {
        HeaderTenantResolver resolver = CreateResolver("X-Custom-Tenant");
        DefaultHttpContext context = ContextWithHeader("X-Tenant-Id", Guid.NewGuid().ToString());

        TenantInfo? result = await resolver.ResolveAsync(context, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }
}
