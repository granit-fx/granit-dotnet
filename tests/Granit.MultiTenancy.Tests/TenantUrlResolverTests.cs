using Granit.MultiTenancy.Options;
using Granit.MultiTenancy.Stores;
using Granit.MultiTenancy.Url;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Tests;

public sealed class TenantUrlResolverTests
{
    private static TenantUrlResolver CreateResolver(
        ICurrentTenant currentTenant, ITenantReader reader, TenantUrlStrategy strategy) =>
        new(currentTenant, reader, Microsoft.Extensions.Options.Options.Create(new MultiTenancyOptions
        {
            UrlStrategy = strategy,
            DomainTemplate = "{0}.example.com",
            UrlScheme = "https",
            FallbackBaseUrl = "https://host.example.com",
        }));

    [Fact]
    public async Task ResolveBaseUrlAsync_NullTenant_ReturnsFallbackEvenUnderSubdomainStrategy()
    {
        // A host account carries TenantId == null. It must resolve the static fallback
        // (host) URL, not a tenant-derived one — regardless of the strategy.
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        ITenantReader reader = Substitute.For<ITenantReader>();
        TenantUrlResolver resolver = CreateResolver(currentTenant, reader, TenantUrlStrategy.Subdomain);

        string url = await resolver.ResolveBaseUrlAsync((Guid?)null, TestContext.Current.CancellationToken);

        url.ShouldBe("https://host.example.com");
        await reader.DidNotReceiveWithAnyArgs().FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveBaseUrlAsync_ExplicitTenant_IgnoresAmbientContext()
    {
        // Ambient context points at a DIFFERENT tenant: the overload must use the
        // tenant it is given, proving background handlers resolve the account's own URL.
        var ambientTenant = Guid.NewGuid();
        var accountTenant = Guid.NewGuid();

        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.IsAvailable.Returns(true);
        currentTenant.Id.Returns(ambientTenant);

        ITenantReader reader = Substitute.For<ITenantReader>();
        reader.FindByIdAsync(accountTenant, Arg.Any<CancellationToken>())
            .Returns(new TenantData(accountTenant, "Acme", "acme", null, true, null, DateTimeOffset.UnixEpoch));

        TenantUrlResolver resolver = CreateResolver(currentTenant, reader, TenantUrlStrategy.Subdomain);

        string url = await resolver.ResolveBaseUrlAsync(accountTenant, TestContext.Current.CancellationToken);

        url.ShouldBe("https://acme.example.com");
        await reader.DidNotReceive().FindByIdAsync(ambientTenant, Arg.Any<CancellationToken>());
    }
}
