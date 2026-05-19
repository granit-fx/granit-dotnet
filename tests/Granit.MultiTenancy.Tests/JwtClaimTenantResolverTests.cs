// =============================================================================
// JwtClaimTenantResolverTests - Unit tests for the JWT tenant resolver
// =============================================================================

using System.Security.Claims;
using Granit.MultiTenancy.Options;
using Granit.MultiTenancy.Resolvers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Tests;

public sealed class JwtClaimTenantResolverTests
{
    private static JwtClaimTenantResolver CreateResolver(string claimType = "tenant_id")
    {
        IOptions<MultiTenancyOptions> options = Microsoft.Extensions.Options.Options.Create(new MultiTenancyOptions
        {
            TenantIdClaimType = claimType
        });
        return new JwtClaimTenantResolver(options);
    }

    private static DefaultHttpContext ContextWithClaim(string claimType, string claimValue)
    {
        DefaultHttpContext context = new();
        ClaimsIdentity identity = new([new Claim(claimType, claimValue)], "test");
        context.User = new ClaimsPrincipal(identity);
        return context;
    }

    [Fact]
    public void Order_Is_200()
    {
        JwtClaimTenantResolver resolver = CreateResolver();
        resolver.Order.ShouldBe(200);
    }

    [Fact]
    public async Task ValidClaim_Returns_TenantInfo()
    {
        JwtClaimTenantResolver resolver = CreateResolver();
        var tenantId = Guid.NewGuid();
        DefaultHttpContext context = ContextWithClaim("tenant_id", tenantId.ToString());

        TenantInfo? result = await resolver.ResolveAsync(context, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result!.Id.ShouldBe(tenantId);
    }

    [Fact]
    public async Task MissingClaim_Returns_Null()
    {
        JwtClaimTenantResolver resolver = CreateResolver();
        DefaultHttpContext context = new();

        TenantInfo? result = await resolver.ResolveAsync(context, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task EmptyClaim_Returns_Null()
    {
        JwtClaimTenantResolver resolver = CreateResolver();
        DefaultHttpContext context = ContextWithClaim("tenant_id", string.Empty);

        TenantInfo? result = await resolver.ResolveAsync(context, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task InvalidGuidClaim_Returns_Null()
    {
        JwtClaimTenantResolver resolver = CreateResolver();
        DefaultHttpContext context = ContextWithClaim("tenant_id", "not-a-guid");

        TenantInfo? result = await resolver.ResolveAsync(context, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task CustomClaimType_Is_Respected()
    {
        JwtClaimTenantResolver resolver = CreateResolver("custom_tenant");
        var tenantId = Guid.NewGuid();
        DefaultHttpContext context = ContextWithClaim("custom_tenant", tenantId.ToString());

        TenantInfo? result = await resolver.ResolveAsync(context, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result!.Id.ShouldBe(tenantId);
    }

    [Fact]
    public async Task WrongClaimType_Returns_Null()
    {
        JwtClaimTenantResolver resolver = CreateResolver("custom_tenant");
        DefaultHttpContext context = ContextWithClaim("tenant_id", Guid.NewGuid().ToString());

        TenantInfo? result = await resolver.ResolveAsync(context, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task UnauthenticatedUser_Returns_Null()
    {
        JwtClaimTenantResolver resolver = CreateResolver();
        DefaultHttpContext context = new()
        {
            User = new ClaimsPrincipal()
        };

        TenantInfo? result = await resolver.ResolveAsync(context, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }
}
