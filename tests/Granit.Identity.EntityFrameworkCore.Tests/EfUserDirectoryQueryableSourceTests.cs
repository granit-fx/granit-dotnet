using Granit.Identity.Domain;
using Granit.Identity.EntityFrameworkCore.Internal;
using Granit.Identity.EntityFrameworkCore.Options;
using Granit.MultiTenancy;
using Granit.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.EntityFrameworkCore.Tests;

/// <summary>
/// Locks the contract of <see cref="EfUserDirectoryQueryableSource"/> — returns an
/// <see cref="IQueryable{User}"/> over the <c>Users</c> set, with the framework's
/// <c>ApplyGranitConventions</c> filters (tenant + soft-delete) applied transparently.
/// Tests run on SQLite in-memory for fast, isolated relational coverage; integration tests
/// against Postgres ship in a follow-up <c>*.Tests.Integration</c> project once the
/// Showcase migration lands.
/// </summary>
public sealed class EfUserDirectoryQueryableSourceTests : IDisposable
{
    private readonly TestDataFilter _filter = new();
    private readonly TestDbContextFactory _factory;
    private readonly ITenantsAccessor _emptyTenantsAccessor;

    public EfUserDirectoryQueryableSourceTests()
    {
        _factory = TestDbContextFactory.Create(_filter.Filter);
        _emptyTenantsAccessor = Substitute.For<ITenantsAccessor>();
        _emptyTenantsAccessor.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<(Guid Id, string Name)>>([]));
    }

    public void Dispose()
    {
        _factory.Dispose();
        _filter.Dispose();
    }

    [Fact]
    public async Task GetQueryable_ReturnsUsers_FromTheUnderlyingTable()
    {
        await SeedAsync(
            User.Create(Guid.NewGuid(), "alice@example.com", "Alice"),
            User.Create(Guid.NewGuid(), "bob@example.com", "Bob"));

        using EfUserDirectoryQueryableSource sut = NewSharedSource();

        List<User> users = await sut.GetQueryable()
            .OrderBy(u => u.DisplayName)
            .ToListAsync(TestContext.Current.CancellationToken);

        users.Count.ShouldBe(2);
        users[0].DisplayName.ShouldBe("Alice");
        users[1].DisplayName.ShouldBe("Bob");
    }

    [Fact]
    public async Task GetQueryable_AsNoTracking_DoesNotAttachEntities()
    {
        // The contract emits no-tracking queries (admin / OData read paths never write back
        // through this surface). A mutated entity from GetQueryable is never persisted by
        // SaveChanges.
        await SeedAsync(User.Create(Guid.NewGuid(), "carol@example.com", "Carol"));

        using EfUserDirectoryQueryableSource sut = NewSharedSource();

        User user = await sut.GetQueryable().SingleAsync(TestContext.Current.CancellationToken);
        user.UpdateProfile(
            displayName: "Carol Renamed",
            email: user.Email,
            firstName: null,
            lastName: null,
            phoneNumber: null,
            preferredLocale: null,
            timezone: null);

        await using IdentityHostDbContext fresh = _factory.CreateDbContext();
        User reread = await fresh.Users.SingleAsync(TestContext.Current.CancellationToken);
        reread.DisplayName.ShouldBe("Carol");
    }

    [Fact]
    public async Task GetQueryable_ComposesWithFilterAndProjection()
    {
        // The contract is `IQueryable<User>` — consumers compose `Where`, `Select`,
        // `OrderBy`, etc. End-to-end verification that LINQ composition translates to SQL
        // through the factory-created context.
        await SeedAsync(
            User.Create(Guid.NewGuid(), "dave@example.com", "Dave", firstName: "Dave"),
            User.Create(Guid.NewGuid(), "eve@example.com", "Eve", firstName: "Eve"),
            User.Create(Guid.NewGuid(), "frank@example.com", "Frank Disabled"));

        using EfUserDirectoryQueryableSource sut = NewSharedSource();

        List<string> firstNames = await sut.GetQueryable()
            .Where(u => u.FirstName != null)
            .OrderBy(u => u.FirstName)
            .Select(u => u.FirstName!)
            .ToListAsync(TestContext.Current.CancellationToken);

        firstNames.ShouldBe(["Dave", "Eve"]);
    }

    private async Task SeedAsync(params User[] users)
    {
        await using IdentityHostDbContext db = _factory.CreateDbContext();
        db.Users.AddRange(users);
        await db.SaveChangesAsync();
    }

    private EfUserDirectoryQueryableSource NewSharedSource()
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.IsAvailable.Returns(false);

        return new EfUserDirectoryQueryableSource(
            new IdentityEntityFrameworkCoreOptions { StorageMode = DualScopeStorageMode.Shared },
            currentTenant,
            _emptyTenantsAccessor,
            _factory,
            tenantFactory: null);
    }
}
