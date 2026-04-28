using Granit.DataFiltering;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Parties.Domain;
using Granit.Parties.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Shouldly;
using Xunit;

namespace Granit.Parties.EntityFrameworkCore.Tests.Internal;

[Collection(PartiesDbSerialGroup.Name)]
public sealed class EfDefaultContactResolverTests : IAsyncDisposable
{
    private readonly DataFilter _filter = new();
    private readonly string _databaseName = $"parties-resolver-{Guid.NewGuid()}";
    private readonly InMemoryDatabaseRoot _dbRoot = new();

    public async ValueTask DisposeAsync()
    {
        StubCurrentTenant t = new();
        await using PartiesDbContext db = new(BuildOptions(), t, _filter);
        await db.Database.EnsureDeletedAsync();
    }

    private DbContextOptions<PartiesDbContext> BuildOptions() =>
        new DbContextOptionsBuilder<PartiesDbContext>()
            .UseInMemoryDatabase(_databaseName, _dbRoot)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .EnableServiceProviderCaching(false)
            .Options;

    private (EfDefaultPartyResolver Resolver, ScopedFactory Factory, StubCurrentTenant Tenant)
        BuildResolver(Guid? tenantId)
    {
        StubCurrentTenant tenant = new();
        if (tenantId is { } id) { tenant.Set(id); }
        ScopedFactory factory = new(BuildOptions(), tenant, _filter);
        return (new EfDefaultPartyResolver(factory, _filter), factory, tenant);
    }

    private async Task<Party> SeedHostContactForTenantAsync(Guid tenantId, string name = "Tenant-as-customer")
    {
        var party = Party.Create(Guid.NewGuid(), null, PartyKind.Company, name, "EUR");
        party.AddExternalMapping(Guid.NewGuid(), PartyExternalProviderNames.Tenant, tenantId.ToString());

        (_, ScopedFactory factory, _) = BuildResolver(tenantId: null);
        using IDisposable bypass = _filter.Disable<IMultiTenant>();
        await using PartiesDbContext db = await factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        db.Parties.Add(party);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return party;
    }

    [Fact]
    public async Task GetDefaultForTenantAsync_LinkedHostContact_ReturnsIt()
    {
        var tenantId = Guid.NewGuid();
        Party seeded = await SeedHostContactForTenantAsync(tenantId);
        (EfDefaultPartyResolver resolver, _, _) = BuildResolver(tenantId: null);

        Party? hit = await resolver.GetDefaultForTenantAsync(tenantId, TestContext.Current.CancellationToken);

        hit.ShouldNotBeNull();
        hit.Id.ShouldBe(seeded.Id);
        hit.TenantId.ShouldBeNull();
        hit.FindExternalId(PartyExternalProviderNames.Tenant).ShouldBe(tenantId.ToString());
    }

    [Fact]
    public async Task GetDefaultForTenantAsync_NoMapping_ReturnsNull()
    {
        (EfDefaultPartyResolver resolver, _, _) = BuildResolver(tenantId: null);

        Party? hit = await resolver.GetDefaultForTenantAsync(
            Guid.NewGuid(), TestContext.Current.CancellationToken);

        hit.ShouldBeNull();
    }

    [Fact]
    public async Task GetDefaultForTenantAsync_TenantContextActive_StillResolvesHostScoped()
    {
        // Resolver must work even when called from inside a tenant scope (tenant code
        // looking up its own representative host-scoped party). The tenant filter
        // would otherwise hide the host-scoped row (TenantId == null != current tenant).
        var tenantId = Guid.NewGuid();
        Party seeded = await SeedHostContactForTenantAsync(tenantId);
        (EfDefaultPartyResolver resolver, _, _) = BuildResolver(tenantId);

        Party? hit = await resolver.GetDefaultForTenantAsync(tenantId, TestContext.Current.CancellationToken);

        hit.ShouldNotBeNull();
        hit.Id.ShouldBe(seeded.Id);
    }

    [Fact]
    public async Task GetDefaultForTenantAsync_OtherProviderMapping_NotMatched()
    {
        // A party whose only external mapping is e.g. "stripe" must not be returned
        // for a tenantId that happens to equal the stripe customer id.
        var tenantId = Guid.NewGuid();
        var party = Party.Create(Guid.NewGuid(), null, PartyKind.Company, "Acme", "EUR");
        party.AddExternalMapping(
            Guid.NewGuid(), PartyExternalProviderNames.Stripe, tenantId.ToString());
        (_, ScopedFactory factory, _) = BuildResolver(tenantId: null);
        using (IDisposable bypass = _filter.Disable<IMultiTenant>())
        {
            await using PartiesDbContext db = await factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
            db.Parties.Add(party);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        (EfDefaultPartyResolver resolver, _, _) = BuildResolver(tenantId: null);
        Party? hit = await resolver.GetDefaultForTenantAsync(tenantId, TestContext.Current.CancellationToken);

        hit.ShouldBeNull();
    }

    [Fact]
    public async Task GetDefaultForTenantAsync_EmptyGuid_Throws()
    {
        (EfDefaultPartyResolver resolver, _, _) = BuildResolver(tenantId: null);

        await Should.ThrowAsync<ArgumentException>(() =>
            resolver.GetDefaultForTenantAsync(Guid.Empty, TestContext.Current.CancellationToken));
    }
}
