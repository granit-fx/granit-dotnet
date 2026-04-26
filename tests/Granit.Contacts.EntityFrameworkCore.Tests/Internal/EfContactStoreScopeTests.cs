using Granit.Contacts.Domain;
using Granit.Contacts.Domain.ValueObjects;
using Granit.Contacts.EntityFrameworkCore.Internal;
using Granit.DataFiltering;
using Granit.Domain;
using Granit.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Shouldly;
using Xunit;

namespace Granit.Contacts.EntityFrameworkCore.Tests.Internal;

/// <summary>
/// Integration tests covering the dual-use design (US #1230): the same <see cref="Contact"/>
/// aggregate must back both host-scoped (<c>TenantId == null</c>) and tenant-scoped
/// (<c>TenantId == &lt;tenant&gt;</c>) usage without consumer code branches.
///
/// Three contacts are seeded with the multi-tenant filter <i>disabled</i> so each row lands
/// at its intended scope. Subsequent reads exercise the filter under each context (tenant A,
/// tenant B, host, no-filter) and assert that <see cref="EfContactStore"/> honours the
/// active scope on every query path — including <c>GetByIdAsync</c>.
/// </summary>
[Collection(ContactsDbSerialGroup.Name)]
public sealed class EfContactStoreScopeTests : IAsyncDisposable
{
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly DataFilter _filter = new();
    private readonly string _databaseName = $"contacts-scope-{Guid.NewGuid()}";
    // Shared in-memory root so the per-scope service providers (each with its own
    // IModelSource and model cache) all read/write the same underlying store.
    private readonly InMemoryDatabaseRoot _dbRoot = new();

    private Contact _hostContact = null!;
    private Contact _tenantAContact = null!;
    private Contact _tenantBContact = null!;

    public async ValueTask DisposeAsync()
    {
        // Each test scope creates its own context; cleanup is per-test via the unique DB name.
        StubCurrentTenant t = new();
        DbContextOptions<ContactsDbContext> opts = BuildOptions();
        await using ContactsDbContext db = new(opts, t, _filter);
        await db.Database.EnsureDeletedAsync();
    }

    private DbContextOptions<ContactsDbContext> BuildOptions() =>
        new DbContextOptionsBuilder<ContactsDbContext>()
            // Share the in-memory store across scopes (different service providers) so a
            // tenant-scoped query reads rows persisted by the host-scoped seeder.
            .UseInMemoryDatabase(_databaseName, _dbRoot)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            // Force a fresh EF service provider per scope so OnModelCreating runs again,
            // re-capturing the current ICurrentTenant in the multi-tenant query-filter
            // expression. Without this, all scopes within a fixture would share a model
            // built with whichever ICurrentTenant happened to be active first.
            .EnableServiceProviderCaching(false)
            .Options;

    /// <summary>
    /// Builds a fresh DbContext + EfContactStore pair pinned to the given tenant. EF Core
    /// captures the <see cref="ICurrentTenant"/> instance into the query-filter expression
    /// at model-build time. To exercise different scopes we therefore build a NEW model
    /// (via a fresh <see cref="UniqueModelCacheKeyFactory"/>) each time, with the tenant
    /// already in the desired state.
    /// </summary>
    private (EfContactStore Store, ScopedFactory Factory) ScopedStore(Guid? tenantId)
    {
        StubCurrentTenant tenant = new();
        if (tenantId is { } id) { tenant.Set(id); }
        ScopedFactory factory = new(BuildOptions(), tenant, _filter);
        return (new EfContactStore(factory, tenant), factory);
    }

    private async Task SeedAsync()
    {
        // Seed via a host-scoped store with the multi-tenant filter disabled so rows land
        // at their intended scope regardless of the active context.
        _hostContact = Contact.Create(Guid.NewGuid(), null, ContactKind.Company, "Host", "EUR");
        _tenantAContact = Contact.Create(Guid.NewGuid(), _tenantA, ContactKind.Company, "TenantA", "EUR");
        _tenantBContact = Contact.Create(Guid.NewGuid(), _tenantB, ContactKind.Company, "TenantB", "EUR");

        (_, ScopedFactory factory) = ScopedStore(tenantId: null);
        using IDisposable bypass = _filter.Disable<IMultiTenant>();
        await using ContactsDbContext db = await factory.CreateDbContextAsync();
        db.Contacts.AddRange(_hostContact, _tenantAContact, _tenantBContact);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // ── ListAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_InTenantContext_ReturnsOnlyThatTenantsContacts()
    {
        await SeedAsync();
        (EfContactStore store, _) = ScopedStore(_tenantA);

        IReadOnlyList<Contact> result = await store.ListAsync(TestContext.Current.CancellationToken);

        result.Select(c => c.Name).ShouldBe(["TenantA"]);
    }

    [Fact]
    public async Task ListAsync_InHostContext_ReturnsOnlyHostScopedContacts()
    {
        await SeedAsync();
        (EfContactStore store, _) = ScopedStore(tenantId: null);

        IReadOnlyList<Contact> result = await store.ListAsync(TestContext.Current.CancellationToken);

        result.Select(c => c.Name).ShouldBe(["Host"]);
    }

    [Fact]
    public async Task ListAsync_WithFilterDisabled_ReturnsAllScopes()
    {
        await SeedAsync();
        (EfContactStore store, _) = ScopedStore(_tenantA);

        using IDisposable bypass = _filter.Disable<IMultiTenant>();
        IReadOnlyList<Contact> result = await store.ListAsync(TestContext.Current.CancellationToken);

        result.Select(c => c.Name).ShouldBe(["Host", "TenantA", "TenantB"], ignoreOrder: true);
    }

    // ── GetByIdAsync — must honour the active scope ──────────────────

    [Fact]
    public async Task GetByIdAsync_TenantContext_ReturnsOwnContact()
    {
        await SeedAsync();
        (EfContactStore store, _) = ScopedStore(_tenantA);

        Contact? hit = await store.GetByIdAsync(
            ContactId.Create(_tenantAContact.Id), TestContext.Current.CancellationToken);

        hit.ShouldNotBeNull();
        hit.Id.ShouldBe(_tenantAContact.Id);
    }

    [Fact]
    public async Task GetByIdAsync_TenantContext_HostContact_ReturnsNull()
    {
        await SeedAsync();
        (EfContactStore store, _) = ScopedStore(_tenantA);

        Contact? hit = await store.GetByIdAsync(
            ContactId.Create(_hostContact.Id), TestContext.Current.CancellationToken);

        hit.ShouldBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_TenantContext_OtherTenantContact_ReturnsNull()
    {
        await SeedAsync();
        (EfContactStore store, _) = ScopedStore(_tenantA);

        Contact? hit = await store.GetByIdAsync(
            ContactId.Create(_tenantBContact.Id), TestContext.Current.CancellationToken);

        hit.ShouldBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_HostContext_HostContact_Returns()
    {
        await SeedAsync();
        (EfContactStore store, _) = ScopedStore(tenantId: null);

        Contact? hit = await store.GetByIdAsync(
            ContactId.Create(_hostContact.Id), TestContext.Current.CancellationToken);

        hit.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_HostContext_TenantContact_ReturnsNull()
    {
        await SeedAsync();
        (EfContactStore store, _) = ScopedStore(tenantId: null);

        Contact? hit = await store.GetByIdAsync(
            ContactId.Create(_tenantAContact.Id), TestContext.Current.CancellationToken);

        hit.ShouldBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WithFilterDisabled_FindsAcrossScopes()
    {
        await SeedAsync();
        (EfContactStore store, _) = ScopedStore(_tenantA);

        using IDisposable bypass = _filter.Disable<IMultiTenant>();
        Contact? host = await store.GetByIdAsync(
            ContactId.Create(_hostContact.Id), TestContext.Current.CancellationToken);
        Contact? other = await store.GetByIdAsync(
            ContactId.Create(_tenantBContact.Id), TestContext.Current.CancellationToken);

        host.ShouldNotBeNull();
        other.ShouldNotBeNull();
    }

}
