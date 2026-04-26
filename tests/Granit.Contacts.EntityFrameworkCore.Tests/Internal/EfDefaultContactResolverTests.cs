using Granit.Contacts.Domain;
using Granit.Contacts.EntityFrameworkCore.Internal;
using Granit.DataFiltering;
using Granit.Domain;
using Granit.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Shouldly;
using Xunit;

namespace Granit.Contacts.EntityFrameworkCore.Tests.Internal;

[Collection(ContactsDbSerialGroup.Name)]
public sealed class EfDefaultContactResolverTests : IAsyncDisposable
{
    private readonly DataFilter _filter = new();
    private readonly string _databaseName = $"contacts-resolver-{Guid.NewGuid()}";
    private readonly InMemoryDatabaseRoot _dbRoot = new();

    public async ValueTask DisposeAsync()
    {
        StubCurrentTenant t = new();
        await using ContactsDbContext db = new(BuildOptions(), t, _filter);
        await db.Database.EnsureDeletedAsync();
    }

    private DbContextOptions<ContactsDbContext> BuildOptions() =>
        new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(_databaseName, _dbRoot)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .EnableServiceProviderCaching(false)
            .Options;

    private (EfDefaultContactResolver Resolver, ScopedFactory Factory, StubCurrentTenant Tenant)
        BuildResolver(Guid? tenantId)
    {
        StubCurrentTenant tenant = new();
        if (tenantId is { } id) { tenant.Set(id); }
        ScopedFactory factory = new(BuildOptions(), tenant, _filter);
        return (new EfDefaultContactResolver(factory, _filter), factory, tenant);
    }

    private async Task<Contact> SeedHostContactForTenantAsync(Guid tenantId, string name = "Tenant-as-customer")
    {
        var contact = Contact.Create(Guid.NewGuid(), null, ContactKind.Company, name, "EUR");
        contact.AddExternalMapping(Guid.NewGuid(), ContactExternalProviderNames.Tenant, tenantId.ToString());

        (_, ScopedFactory factory, _) = BuildResolver(tenantId: null);
        using IDisposable bypass = _filter.Disable<IMultiTenant>();
        await using ContactsDbContext db = await factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        db.Contacts.Add(contact);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return contact;
    }

    [Fact]
    public async Task GetDefaultForTenantAsync_LinkedHostContact_ReturnsIt()
    {
        var tenantId = Guid.NewGuid();
        Contact seeded = await SeedHostContactForTenantAsync(tenantId);
        (EfDefaultContactResolver resolver, _, _) = BuildResolver(tenantId: null);

        Contact? hit = await resolver.GetDefaultForTenantAsync(tenantId, TestContext.Current.CancellationToken);

        hit.ShouldNotBeNull();
        hit.Id.ShouldBe(seeded.Id);
        hit.TenantId.ShouldBeNull();
        hit.FindExternalId(ContactExternalProviderNames.Tenant).ShouldBe(tenantId.ToString());
    }

    [Fact]
    public async Task GetDefaultForTenantAsync_NoMapping_ReturnsNull()
    {
        (EfDefaultContactResolver resolver, _, _) = BuildResolver(tenantId: null);

        Contact? hit = await resolver.GetDefaultForTenantAsync(
            Guid.NewGuid(), TestContext.Current.CancellationToken);

        hit.ShouldBeNull();
    }

    [Fact]
    public async Task GetDefaultForTenantAsync_TenantContextActive_StillResolvesHostScoped()
    {
        // Resolver must work even when called from inside a tenant scope (tenant code
        // looking up its own representative host-scoped contact). The tenant filter
        // would otherwise hide the host-scoped row (TenantId == null != current tenant).
        var tenantId = Guid.NewGuid();
        Contact seeded = await SeedHostContactForTenantAsync(tenantId);
        (EfDefaultContactResolver resolver, _, _) = BuildResolver(tenantId);

        Contact? hit = await resolver.GetDefaultForTenantAsync(tenantId, TestContext.Current.CancellationToken);

        hit.ShouldNotBeNull();
        hit.Id.ShouldBe(seeded.Id);
    }

    [Fact]
    public async Task GetDefaultForTenantAsync_OtherProviderMapping_NotMatched()
    {
        // A contact whose only external mapping is e.g. "stripe" must not be returned
        // for a tenantId that happens to equal the stripe customer id.
        var tenantId = Guid.NewGuid();
        var contact = Contact.Create(Guid.NewGuid(), null, ContactKind.Company, "Acme", "EUR");
        contact.AddExternalMapping(
            Guid.NewGuid(), ContactExternalProviderNames.Stripe, tenantId.ToString());
        (_, ScopedFactory factory, _) = BuildResolver(tenantId: null);
        using (IDisposable bypass = _filter.Disable<IMultiTenant>())
        {
            await using ContactsDbContext db = await factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
            db.Contacts.Add(contact);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        (EfDefaultContactResolver resolver, _, _) = BuildResolver(tenantId: null);
        Contact? hit = await resolver.GetDefaultForTenantAsync(tenantId, TestContext.Current.CancellationToken);

        hit.ShouldBeNull();
    }

    [Fact]
    public async Task GetDefaultForTenantAsync_EmptyGuid_Throws()
    {
        (EfDefaultContactResolver resolver, _, _) = BuildResolver(tenantId: null);

        await Should.ThrowAsync<ArgumentException>(() =>
            resolver.GetDefaultForTenantAsync(Guid.Empty, TestContext.Current.CancellationToken));
    }
}
