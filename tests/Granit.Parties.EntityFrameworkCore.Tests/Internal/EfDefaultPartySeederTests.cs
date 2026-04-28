using Granit.DataFiltering;
using Granit.Domain;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Parties.Domain;
using Granit.Parties.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Parties.EntityFrameworkCore.Tests.Internal;

[Collection(PartiesDbSerialGroup.Name)]
public sealed class EfDefaultContactSeederTests : IAsyncDisposable
{
    private readonly DataFilter _filter = new();
    private readonly string _databaseName = $"parties-seeder-{Guid.NewGuid()}";
    private readonly InMemoryDatabaseRoot _dbRoot = new();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();

    public EfDefaultContactSeederTests()
    {
        _guidGenerator.Create().Returns(_ => Guid.NewGuid());
    }

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

    private (EfDefaultPartySeeder Seeder, EfDefaultPartyResolver Resolver, ScopedFactory Factory)
        Build(Guid? tenantId = null)
    {
        StubCurrentTenant tenant = new();
        if (tenantId is { } id) { tenant.Set(id); }
        ScopedFactory factory = new(BuildOptions(), tenant, _filter);
        EfDefaultPartyResolver resolver = new(factory, _filter);
        EfDefaultPartySeeder seeder = new(resolver, factory, _guidGenerator, _filter);
        return (seeder, resolver, factory);
    }

    [Fact]
    public async Task SeedForTenantAsync_NoExisting_CreatesHostScopedContactWithTenantMapping()
    {
        var tenantId = Guid.NewGuid();
        (EfDefaultPartySeeder seeder, _, _) = Build();

        Party result = await seeder.SeedForTenantAsync(
            tenantId, "ACME Inc.", cancellationToken: TestContext.Current.CancellationToken);

        result.TenantId.ShouldBeNull();
        result.Name.ShouldBe("ACME Inc.");
        result.Kind.ShouldBe(PartyKind.Company);
        result.DefaultCurrency.ShouldBe("EUR");
        result.FindExternalId(PartyExternalProviderNames.Tenant).ShouldBe(tenantId.ToString());
    }

    [Fact]
    public async Task SeedForTenantAsync_AlreadySeeded_ReturnsExistingWithoutDuplicating()
    {
        var tenantId = Guid.NewGuid();
        (EfDefaultPartySeeder seeder, _, _) = Build();

        Party first = await seeder.SeedForTenantAsync(
            tenantId, "ACME Inc.", cancellationToken: TestContext.Current.CancellationToken);
        Party second = await seeder.SeedForTenantAsync(
            tenantId, "Different Name", cancellationToken: TestContext.Current.CancellationToken);

        second.Id.ShouldBe(first.Id);
        second.Name.ShouldBe("ACME Inc.", "the existing party must be preserved — second call must not rename");
    }

    [Fact]
    public async Task SeedForTenantAsync_TenantContextActive_StillCreatesHostScoped()
    {
        // The seeder is typically invoked from a Wolverine handler running in the
        // newly-created tenant's scope. The host-scoped row (TenantId == null) must
        // still land correctly even though the active tenant filter would normally
        // hide it from any subsequent read.
        var tenantId = Guid.NewGuid();
        (EfDefaultPartySeeder seeder, _, _) = Build(tenantId);

        Party result = await seeder.SeedForTenantAsync(
            tenantId, "ACME Inc.", cancellationToken: TestContext.Current.CancellationToken);

        result.TenantId.ShouldBeNull();
        result.FindExternalId(PartyExternalProviderNames.Tenant).ShouldBe(tenantId.ToString());
    }

    [Fact]
    public async Task SeedForTenantAsync_EmptyGuid_Throws()
    {
        (EfDefaultPartySeeder seeder, _, _) = Build();

        await Should.ThrowAsync<ArgumentException>(() =>
            seeder.SeedForTenantAsync(Guid.Empty, "ACME", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task SeedForTenantAsync_BlankName_Throws(string name)
    {
        (EfDefaultPartySeeder seeder, _, _) = Build();

        await Should.ThrowAsync<ArgumentException>(() =>
            seeder.SeedForTenantAsync(Guid.NewGuid(), name, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SeedForTenantAsync_CustomCurrency_HonoursIt()
    {
        var tenantId = Guid.NewGuid();
        (EfDefaultPartySeeder seeder, _, _) = Build();

        Party result = await seeder.SeedForTenantAsync(
            tenantId, "Globex", defaultCurrency: "usd",
            cancellationToken: TestContext.Current.CancellationToken);

        result.DefaultCurrency.ShouldBe("USD");
    }
}
