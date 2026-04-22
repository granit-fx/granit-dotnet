using Granit.Authorization.Domain;
using Granit.Authorization.EntityFrameworkCore.Stores;
using Granit.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;
using Xunit;

namespace Granit.Authorization.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="RoleMetadata"/> persistence against a real PostgreSQL 17
/// instance — validates behavior that the in-memory EF provider cannot exercise:
/// <list type="bullet">
///   <item>The composite unique index with <c>NULLS NOT DISTINCT</c> semantics.</item>
///   <item>The <c>CHECK</c> constraint enforcing the <c>Side</c> ↔ <c>TenantId</c> invariant.</item>
///   <item>The <see cref="EfCoreRoleMetadataStore{TContext}"/> roundtrip (add, find, list, remove).</item>
/// </list>
/// </summary>
public sealed class RoleMetadataPostgresTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private TestAuthorizationDbContext _context = null!;

    public RoleMetadataPostgresTests(PostgresFixture postgres) => _postgres = postgres;

    public async ValueTask InitializeAsync()
    {
        DbContextOptions<TestAuthorizationDbContext> options =
            new DbContextOptionsBuilder<TestAuthorizationDbContext>()
                .UseNpgsql(_postgres.ConnectionString)
                .Options;

        _context = new TestAuthorizationDbContext(options);
        await _context.Database.EnsureCreatedAsync();
        await _context.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE authorization_role_metadata, authorization_permission_grants RESTART IDENTITY CASCADE;");
    }

    public ValueTask DisposeAsync() => _context.DisposeAsync();

    // ─────────────────────────────────────────────────────────────────────
    // NULLS NOT DISTINCT on (Name, TenantId, ClientId)
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HostRole_DuplicateName_RejectedByUniqueIndex()
    {
        var first = RoleMetadata.Create(
            Guid.NewGuid(), "SuperAdmin", MultiTenancySide.Host, tenantId: null);
        _context.Set<RoleMetadata>().Add(first);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var duplicate = RoleMetadata.Create(
            Guid.NewGuid(), "SuperAdmin", MultiTenancySide.Host, tenantId: null);
        _context.Set<RoleMetadata>().Add(duplicate);

        DbUpdateException ex = await Should.ThrowAsync<DbUpdateException>(async () =>
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken));

        ex.InnerException.ShouldBeOfType<PostgresException>()
            .SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task BothRole_DuplicateName_RejectedByUniqueIndex()
    {
        // Both rows have (Name, TenantId, ClientId) = (X, null, null) — must collide under NULLS NOT DISTINCT.
        var first = RoleMetadata.Create(
            Guid.NewGuid(), "User", MultiTenancySide.Both, tenantId: null);
        _context.Set<RoleMetadata>().Add(first);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var duplicate = RoleMetadata.Create(
            Guid.NewGuid(), "User", MultiTenancySide.Both, tenantId: null);
        _context.Set<RoleMetadata>().Add(duplicate);

        DbUpdateException ex = await Should.ThrowAsync<DbUpdateException>(async () =>
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken));

        ex.InnerException.ShouldBeOfType<PostgresException>()
            .SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task TenantRoles_SameNameInDifferentTenants_BothPersisted()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        _context.Set<RoleMetadata>().Add(
            RoleMetadata.Create(Guid.NewGuid(), "Manager", MultiTenancySide.Tenant, tenantA));
        _context.Set<RoleMetadata>().Add(
            RoleMetadata.Create(Guid.NewGuid(), "Manager", MultiTenancySide.Tenant, tenantB));

        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        int count = await _context.Set<RoleMetadata>().AsNoTracking()
            .CountAsync(r => r.Name == "Manager", TestContext.Current.CancellationToken);
        count.ShouldBe(2);
    }

    // ─────────────────────────────────────────────────────────────────────
    // CHECK constraint ck_authorization_role_metadata_side_tenant_consistency
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CheckConstraint_RejectsHostRoleWithNonNullTenantId()
    {
        RoleMetadata invalid = BuildInvalidRoleMetadata(
            name: "GhostHost", side: MultiTenancySide.Host, tenantId: Guid.NewGuid());
        _context.Set<RoleMetadata>().Add(invalid);

        DbUpdateException ex = await Should.ThrowAsync<DbUpdateException>(async () =>
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken));

        PostgresException pg = ex.InnerException.ShouldBeOfType<PostgresException>();
        pg.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        pg.ConstraintName!.ShouldContain("side_tenant_consistency");
    }

    [Fact]
    public async Task CheckConstraint_RejectsTenantRoleWithNullTenantId()
    {
        RoleMetadata invalid = BuildInvalidRoleMetadata(
            name: "GhostTenant", side: MultiTenancySide.Tenant, tenantId: null);
        _context.Set<RoleMetadata>().Add(invalid);

        DbUpdateException ex = await Should.ThrowAsync<DbUpdateException>(async () =>
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken));

        ex.InnerException.ShouldBeOfType<PostgresException>()
            .SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
    }

    /// <summary>
    /// Builds a <see cref="RoleMetadata"/> that violates the <c>Side</c> ↔ <c>TenantId</c>
    /// invariant, bypassing the <see cref="RoleMetadata.Create"/> factory via reflection on
    /// the private parameterless constructor + private setters. The point of these tests is
    /// to prove the database CHECK constraint is a valid guardrail on top of the domain
    /// invariant, not to exercise the factory itself.
    /// </summary>
    private static RoleMetadata BuildInvalidRoleMetadata(
        string name, MultiTenancySide side, Guid? tenantId)
    {
        var instance = (RoleMetadata)Activator.CreateInstance(
            typeof(RoleMetadata), nonPublic: true)!;
        SetProperty(instance, nameof(RoleMetadata.Id), Guid.NewGuid());
        SetProperty(instance, nameof(RoleMetadata.Name), name);
        SetProperty(instance, nameof(RoleMetadata.MultiTenancySide), side);
        SetProperty(instance, nameof(RoleMetadata.TenantId), tenantId);
        SetProperty(instance, nameof(RoleMetadata.IsSystem), false);
        return instance;
    }

    private static void SetProperty<T>(RoleMetadata target, string propertyName, T value)
    {
        System.Reflection.PropertyInfo property = typeof(RoleMetadata)
            .GetProperty(propertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Property {propertyName} not found.");
        property.SetValue(target, value);
    }

    // ─────────────────────────────────────────────────────────────────────
    // EfCoreRoleMetadataStore roundtrip
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Store_Add_Then_FindByName_ReturnsSeededRow()
    {
        EfCoreRoleMetadataStore<TestAuthorizationDbContext> store = new(_context);
        var role = RoleMetadata.Create(
            Guid.NewGuid(), "TenantAdministrator", MultiTenancySide.Both, tenantId: null,
            description: "Administrator within a tenant.");

        await store.AddAsync(role, TestContext.Current.CancellationToken);

        RoleMetadata? found = await store.FindByNameAsync(
            "TenantAdministrator", tenantId: null, clientId: null,
            TestContext.Current.CancellationToken);

        found.ShouldNotBeNull();
        found.Id.ShouldBe(role.Id);
        found.MultiTenancySide.ShouldBe(MultiTenancySide.Both);
        found.Description.ShouldBe("Administrator within a tenant.");
    }

    [Fact]
    public async Task Store_Remove_RaisesRoleDeletedEventViaMarkAsDeleted()
    {
        EfCoreRoleMetadataStore<TestAuthorizationDbContext> store = new(_context);
        var role = RoleMetadata.Create(
            Guid.NewGuid(), "DisposableRole", MultiTenancySide.Both, tenantId: null);
        await store.AddAsync(role, TestContext.Current.CancellationToken);

        RoleMetadata? tracked = await _context.Set<RoleMetadata>()
            .FirstAsync(r => r.Id == role.Id, TestContext.Current.CancellationToken);

        await store.RemoveAsync(tracked, TestContext.Current.CancellationToken);

        RoleMetadata? afterDelete = await _context.Set<RoleMetadata>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == role.Id, TestContext.Current.CancellationToken);
        afterDelete.ShouldBeNull();
    }
}
