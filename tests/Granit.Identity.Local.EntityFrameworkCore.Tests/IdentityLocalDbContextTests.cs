using Granit.Identity.Local.Domain;
using Granit.Identity.Local.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Testing.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

#pragma warning disable EF1001 // IdentityLocalDbContext is internal — accessible via InternalsVisibleTo

namespace Granit.Identity.Local.EntityFrameworkCore.Tests;

/// <summary>
/// Validates the isolated <see cref="IdentityLocalDbContext"/>: the self-contained identity model
/// (tables, the unique username index, soft-delete + multi-tenant filters) and the null-is-global
/// tenant semantics its groups inherit.
/// </summary>
public sealed class IdentityLocalDbContextTests : IAsyncLifetime
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly FakeCurrentTenant _tenant = new();
    private SqliteConnection _connection = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync(TestContext.Current.CancellationToken);
        await using IdentityLocalDbContext ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public void Model_MapsUsersTable_WithUniqueUserNameIndexAndFilters()
    {
        using IdentityLocalDbContext ctx = NewContext();
        IEntityType users = ctx.Model.FindEntityType(typeof(LocalIdentity))!;

        users.GetTableName().ShouldBe("openiddict_users");
        users.GetIndexes().ShouldContain(i => i.IsUnique && i.GetDatabaseName() == "UserNameIndex");
        users.GetDeclaredQueryFilters().Select(f => f.Key)
            .ShouldBe([GranitFilterNames.SoftDelete, GranitFilterNames.MultiTenant], ignoreOrder: true);
    }

    [Fact]
    public async Task GlobalGroup_IsVisibleUnderEveryTenant()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await SeedGroupAsync(tenantId: null, name: "global", ct);
        await SeedGroupAsync(tenantId: TenantA, name: "a-only", ct);

        _tenant.Id = TenantA;
        await using (IdentityLocalDbContext ctx = NewContext())
        {
            List<string> names = await ctx.UserGroups.Select(g => g.Name).ToListAsync(ct);
            names.ShouldBe(["global", "a-only"], ignoreOrder: true);
        }

        _tenant.Id = TenantB;
        await using (IdentityLocalDbContext ctx = NewContext())
        {
            List<string> names = await ctx.UserGroups.Select(g => g.Name).ToListAsync(ct);
            names.ShouldBe(["global"]);
        }
    }

    private async Task SeedGroupAsync(Guid? tenantId, string name, CancellationToken ct)
    {
        await using IdentityLocalDbContext ctx = NewContext();
        ctx.UserGroups.Add(new GranitUserGroup { Id = Guid.NewGuid(), Name = name, TenantId = tenantId });
        await ctx.SaveChangesAsync(ct);
    }

    private IdentityLocalDbContext NewContext()
    {
        DbContextOptions<IdentityLocalDbContext> options =
            new DbContextOptionsBuilder<IdentityLocalDbContext>()
                .UseSqlite(_connection)
                .Options;
        return new IdentityLocalDbContext(options, _tenant);
    }
}

#pragma warning restore EF1001
