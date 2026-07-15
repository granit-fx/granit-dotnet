using Granit.DataFiltering;
using Granit.Domain;
using Granit.OpenIddict.EntityFrameworkCore.Entities;
using Granit.OpenIddict.EntityFrameworkCore.Internal;
using Granit.Testing.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.EntityFrameworkCore.Tests;

/// <summary>
/// The multi-tenant query filter on OpenIddict entities implements <em>null-is-global</em>:
/// a row with <c>TenantId == null</c> belongs to no tenant and is visible to every tenant
/// (and to anonymous requests), while a tenant-owned row is invisible to other tenants.
/// </summary>
/// <remarks>
/// Exercised against SQLite so the filter's SQL translation — not merely its expression
/// tree — is validated. The previous filter (<c>TenantId == CurrentTenantId</c> only, no
/// null disjunct) hid every global application the moment a tenant scope was active, which
/// is why consumers disabled the filter ad hoc. See <see cref="OpenIddictDbContext"/>.
/// </remarks>
public sealed class OpenIddictMultiTenantFilterTests : IAsyncLifetime
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly FakeCurrentTenant _tenant = new();
    private readonly MutableDataFilter _dataFilter = new();
    private SqliteConnection _connection = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync(TestContext.Current.CancellationToken);
        await SeedApplicationsAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task GlobalApplication_IsVisible_UnderEveryTenant()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        _tenant.Id = TenantA;
        await using (OpenIddictDbContext ctx = NewContext())
        {
            List<string?> clientIds = await ctx.Set<GranitOpenIddictApplication>()
                .Select(a => a.ClientId).ToListAsync(ct);
            clientIds.ShouldBe(["global", "tenant-a"], ignoreOrder: true);
        }

        _tenant.Id = TenantB;
        await using (OpenIddictDbContext ctx = NewContext())
        {
            List<string?> clientIds = await ctx.Set<GranitOpenIddictApplication>()
                .Select(a => a.ClientId).ToListAsync(ct);
            clientIds.ShouldBe(["global", "tenant-b"], ignoreOrder: true);
        }
    }

    [Fact]
    public async Task TenantOwnedApplication_IsInvisible_ToOtherTenant()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _tenant.Id = TenantA;

        await using OpenIddictDbContext ctx = NewContext();
        bool tenantBVisible = await ctx.Set<GranitOpenIddictApplication>()
            .AnyAsync(a => a.ClientId == "tenant-b", ct);

        tenantBVisible.ShouldBeFalse("tenant B's application must not leak into tenant A's scope");
    }

    [Fact]
    public async Task AnonymousRequest_SeesOnlyGlobalApplications()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _tenant.Id = null; // no tenant scope

        await using OpenIddictDbContext ctx = NewContext();
        List<string?> clientIds = await ctx.Set<GranitOpenIddictApplication>()
            .Select(a => a.ClientId).ToListAsync(ct);

        clientIds.ShouldBe(["global"]);
    }

    [Fact]
    public async Task FilterDisabled_SeesAllApplications()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _tenant.Id = TenantA;
        _dataFilter.SetEnabled<IMultiTenant>(false);

        await using OpenIddictDbContext ctx = NewContext();
        List<string?> clientIds = await ctx.Set<GranitOpenIddictApplication>()
            .Select(a => a.ClientId).ToListAsync(ct);

        clientIds.ShouldBe(["global", "tenant-a", "tenant-b"], ignoreOrder: true);
    }

    private OpenIddictDbContext NewContext()
    {
        DbContextOptions<OpenIddictDbContext> options =
            new DbContextOptionsBuilder<OpenIddictDbContext>()
                .UseSqlite(_connection)
                .Options;
        return new OpenIddictDbContext(options, _tenant, _dataFilter);
    }

    private async Task SeedApplicationsAsync(CancellationToken ct)
    {
        // Inserts are not filtered — seed under the disabled filter to keep intent explicit.
        _dataFilter.SetEnabled<IMultiTenant>(false);
        await using OpenIddictDbContext ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync(ct);

        ctx.Set<GranitOpenIddictApplication>().AddRange(
            new GranitOpenIddictApplication { Id = Guid.NewGuid(), ClientId = "global", TenantId = null },
            new GranitOpenIddictApplication { Id = Guid.NewGuid(), ClientId = "tenant-a", TenantId = TenantA },
            new GranitOpenIddictApplication { Id = Guid.NewGuid(), ClientId = "tenant-b", TenantId = TenantB });
        await ctx.SaveChangesAsync(ct);

        _dataFilter.SetEnabled<IMultiTenant>(true);
    }

    /// <summary>Minimal mutable <see cref="IDataFilter"/> for toggling the multi-tenant filter in-test.</summary>
    private sealed class MutableDataFilter : IDataFilter
    {
        private readonly Dictionary<Type, bool> _state = [];

        public void SetEnabled<TFilter>(bool enabled) => _state[typeof(TFilter)] = enabled;

        public bool IsEnabled<TFilter>() where TFilter : class =>
            !_state.TryGetValue(typeof(TFilter), out bool value) || value;

        public IDisposable Disable<TFilter>() where TFilter : class
        {
            _state[typeof(TFilter)] = false;
            return NullScope.Instance;
        }

        public IDisposable Enable<TFilter>() where TFilter : class
        {
            _state[typeof(TFilter)] = true;
            return NullScope.Instance;
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
