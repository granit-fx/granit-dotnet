using Granit.MultiTenancy.Options;
using Granit.MultiTenancy.Resolvers;
using Granit.MultiTenancy.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Tests;

public sealed class DomainTenantResolverTests
{
    // ── ExtractIdentifier unit tests ─────────────────────────────────

    [Theory]
    [InlineData("acme.example.com", "{0}.example.com", "acme")]
    [InlineData("ACME.Example.COM", "{0}.example.com", "ACME")]
    [InlineData("my-tenant.app.local", "{0}.app.local", "my-tenant")]
    [InlineData("tenant1.sub.example.com", "{0}.sub.example.com", "tenant1")]
    public void ExtractIdentifier_ValidHost_ReturnsIdentifier(
        string host, string template, string expected) =>
        DomainTenantResolver.ExtractIdentifier(host, template).ShouldBe(expected);

    [Theory]
    [InlineData("example.com", "{0}.example.com")]
    [InlineData("other.domain.com", "{0}.example.com")]
    [InlineData("", "{0}.example.com")]
    [InlineData("acme.example.com", "no-placeholder.com")]
    public void ExtractIdentifier_NoMatch_ReturnsNull(string host, string template) =>
        DomainTenantResolver.ExtractIdentifier(host, template).ShouldBeNull();

    // ── ResolveAsync integration tests ───────────────────────────────

    [Fact]
    public async Task ResolveAsync_MatchingTenant_ReturnsTenantInfo()
    {
        var tenantId = Guid.NewGuid();
        var tenantData = new TenantData(tenantId, "Acme", "acme", null, true, null, DateTimeOffset.UtcNow);

        ITenantReader reader = Substitute.For<ITenantReader>();
        reader.FindByIdentifierAsync("acme", Arg.Any<CancellationToken>())
            .Returns(tenantData);

        DomainTenantResolver sut = CreateResolver(reader, "{0}.example.com");
        HttpContext ctx = CreateHttpContext("acme.example.com");

        TenantInfo? result = await sut.ResolveAsync(ctx, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(tenantId);
        result.Name.ShouldBe("Acme");
        result.Identifier.ShouldBe("acme");
    }

    [Fact]
    public async Task ResolveAsync_InactiveTenant_ReturnsNull()
    {
        var tenantData = new TenantData(Guid.NewGuid(), "Acme", "acme", null, false, null, DateTimeOffset.UtcNow);

        ITenantReader reader = Substitute.For<ITenantReader>();
        reader.FindByIdentifierAsync("acme", Arg.Any<CancellationToken>())
            .Returns(tenantData);

        DomainTenantResolver sut = CreateResolver(reader, "{0}.example.com");
        HttpContext ctx = CreateHttpContext("acme.example.com");

        TenantInfo? result = await sut.ResolveAsync(ctx, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_NoDomainTemplate_ReturnsNull()
    {
        ITenantReader reader = Substitute.For<ITenantReader>();
        DomainTenantResolver sut = CreateResolver(reader, null);
        HttpContext ctx = CreateHttpContext("acme.example.com");

        TenantInfo? result = await sut.ResolveAsync(ctx, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_UnknownSubdomain_ReturnsNull()
    {
        ITenantReader reader = Substitute.For<ITenantReader>();
        reader.FindByIdentifierAsync("unknown", Arg.Any<CancellationToken>())
            .Returns((TenantData?)null);

        DomainTenantResolver sut = CreateResolver(reader, "{0}.example.com");
        HttpContext ctx = CreateHttpContext("unknown.example.com");

        TenantInfo? result = await sut.ResolveAsync(ctx, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    private static DomainTenantResolver CreateResolver(ITenantReader reader, string? template) =>
        new(reader, Microsoft.Extensions.Options.Options.Create(new MultiTenancyOptions { DomainTemplate = template }));

    private static DefaultHttpContext CreateHttpContext(string host)
    {
        DefaultHttpContext ctx = new();
        ctx.Request.Host = new HostString(host);
        return ctx;
    }
}
