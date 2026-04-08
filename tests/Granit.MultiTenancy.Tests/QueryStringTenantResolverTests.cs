using Granit.MultiTenancy.Options;
using Granit.MultiTenancy.Resolvers;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Tests;

public sealed class QueryStringTenantResolverTests
{
    [Fact]
    public async Task ResolveAsync_ValidGuid_ReturnsTenantInfo()
    {
        var tenantId = Guid.NewGuid();
        QueryStringTenantResolver sut = CreateResolver("__tenant");
        HttpContext ctx = CreateHttpContext($"?__tenant={tenantId}");

        TenantInfo? result = await sut.ResolveAsync(ctx, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(tenantId);
    }

    [Fact]
    public async Task ResolveAsync_NoQueryParam_ReturnsNull()
    {
        QueryStringTenantResolver sut = CreateResolver("__tenant");
        HttpContext ctx = CreateHttpContext("");

        TenantInfo? result = await sut.ResolveAsync(ctx, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_InvalidGuid_ReturnsNull()
    {
        QueryStringTenantResolver sut = CreateResolver("__tenant");
        HttpContext ctx = CreateHttpContext("?__tenant=not-a-guid");

        TenantInfo? result = await sut.ResolveAsync(ctx, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_DisabledWhenParamNameNull_ReturnsNull()
    {
        QueryStringTenantResolver sut = CreateResolver(null);
        var tenantId = Guid.NewGuid();
        HttpContext ctx = CreateHttpContext($"?__tenant={tenantId}");

        TenantInfo? result = await sut.ResolveAsync(ctx, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_CustomParamName_Works()
    {
        var tenantId = Guid.NewGuid();
        QueryStringTenantResolver sut = CreateResolver("tid");
        HttpContext ctx = CreateHttpContext($"?tid={tenantId}");

        TenantInfo? result = await sut.ResolveAsync(ctx, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(tenantId);
    }

    private static QueryStringTenantResolver CreateResolver(string? paramName) =>
        new(Microsoft.Extensions.Options.Options.Create(
            new MultiTenancyOptions { QueryStringParamName = paramName }));

    private static DefaultHttpContext CreateHttpContext(string queryString)
    {
        DefaultHttpContext ctx = new();
        ctx.Request.QueryString = new QueryString(queryString);
        return ctx;
    }
}
