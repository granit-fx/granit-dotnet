using Granit.Identity.Domain;
using Granit.Identity.EntityFrameworkCore.Internal;
using Granit.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Identity.EntityFrameworkCore.Tests;

/// <summary>
/// Locks the contract of <see cref="EfUserDirectoryWriter"/> — inserts and hard-deletes
/// <see cref="User"/> rows through an <see cref="IdentityContextResolver"/> wired in
/// <see cref="DualScopeStorageMode.Shared"/> mode. SQLite in-memory keeps the test fast
/// and isolated per fact.
/// </summary>
public sealed class EfUserDirectoryWriterTests : IDisposable
{
    private readonly TestDataFilter _filter = new();
    private readonly TestDbContextFactory _factory;
    private readonly IdentityContextResolver _resolver;

    public EfUserDirectoryWriterTests()
    {
        _factory = TestDbContextFactory.Create(_filter.Filter);
        _resolver = new IdentityContextResolver(
            DualScopeStorageMode.Shared,
            hostFactory: _factory,
            tenantFactory: null);
    }

    public void Dispose()
    {
        _factory.Dispose();
        _filter.Dispose();
    }

    [Fact]
    public async Task CreateAsync_PersistsUserWithCallerSuppliedId()
    {
        EfUserDirectoryWriter writer = new(_resolver);
        var id = Guid.NewGuid();
        var user = User.Create(id, "alice@example.com", "Alice");

        await writer.CreateAsync(user, TestContext.Current.CancellationToken);

        await using IdentityHostDbContext fresh = _factory.CreateDbContext();
        User reread = await fresh.Users.SingleAsync(TestContext.Current.CancellationToken);
        reread.Id.ShouldBe(id);
        reread.Email.ShouldBe("alice@example.com");
        reread.DisplayName.ShouldBe("Alice");
    }

    [Fact]
    public async Task DeleteAsync_HardDeletesTheRow()
    {
        // The User aggregate is NOT soft-deletable (per ADR-051 the canonical user is the
        // GDPR drop point) — DeleteAsync must erase the row entirely, not flip an
        // IsDeleted flag.
        EfUserDirectoryWriter writer = new(_resolver);
        var id = Guid.NewGuid();
        await writer.CreateAsync(User.Create(id, "bob@example.com", "Bob"), TestContext.Current.CancellationToken);

        await writer.DeleteAsync(id, TestContext.Current.CancellationToken);

        await using IdentityHostDbContext fresh = _factory.CreateDbContext();
        bool exists = await fresh.Users.AnyAsync(u => u.Id == id, TestContext.Current.CancellationToken);
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_OnMissingId_IsNoOp()
    {
        // Compensating delete must tolerate the case where the User row was never created
        // (e.g. the upstream LocalIdentity insert failed before the writer was even called).
        EfUserDirectoryWriter writer = new(_resolver);

        await Should.NotThrowAsync(() =>
            writer.DeleteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }
}
