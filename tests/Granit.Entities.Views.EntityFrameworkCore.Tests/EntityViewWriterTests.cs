// =============================================================================
// Tests - EntityViewWriter (permission-gated CRUD)
// =============================================================================

using System.Text.Json.Nodes;
using Granit.Authorization;
using Granit.Entities.Views.Domain;
using Granit.Entities.Views.EntityFrameworkCore.Internal;
using Granit.Users;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Entities.Views.EntityFrameworkCore.Tests;

public sealed class EntityViewWriterTests
{
    private static readonly Guid OwnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task CreateAsync_RequiresAuthenticatedUser()
    {
        using TestFixture fixture = new(userId: null);
        await Should.ThrowAsync<UnauthorizedAccessException>(() =>
            fixture.Writer.CreateAsync(NewCreateRequest(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateAsync_RequiresCreatePermission()
    {
        using TestFixture fixture = new(userId: OwnerId, grantedPermissions: []);
        await Should.ThrowAsync<UnauthorizedAccessException>(() =>
            fixture.Writer.CreateAsync(NewCreateRequest(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateAsync_PersistsView_WhenAuthorized()
    {
        using TestFixture fixture = new(userId: OwnerId, grantedPermissions: [EntityViewPermissions.Create]);

        EntityViewDescriptor descriptor = await fixture.Writer.CreateAsync(
            NewCreateRequest(), TestContext.Current.CancellationToken);

        descriptor.OwnerId.ShouldBe(OwnerId);
        descriptor.Visibility.ShouldBe(EntityViewVisibility.Personal);
        (await fixture.DbContext.EntityViews.CountAsync(TestContext.Current.CancellationToken))
            .ShouldBe(1);
    }

    [Fact]
    public async Task UpdateAsync_OwnerCanUpdate_WithoutManagePermission()
    {
        using TestFixture fixture = new(userId: OwnerId, grantedPermissions: [EntityViewPermissions.Create]);
        EntityViewDescriptor created = await fixture.Writer.CreateAsync(
            NewCreateRequest(), TestContext.Current.CancellationToken);

        EntityViewDescriptor updated = await fixture.Writer.UpdateAsync(
            created.Id,
            new EntityViewUpdateRequest("Renamed", "New desc", "icon", new JsonObject()),
            TestContext.Current.CancellationToken);

        updated.Name.ShouldBe("Renamed");
    }

    [Fact]
    public async Task UpdateAsync_NonOwner_RequiresManagePermission()
    {
        string sharedDb = Guid.NewGuid().ToString();
        using TestFixture seedFixture = new(userId: OwnerId, grantedPermissions: [EntityViewPermissions.Create], dbName: sharedDb);
        EntityViewDescriptor created = await seedFixture.Writer.CreateAsync(
            NewCreateRequest(), TestContext.Current.CancellationToken);

        using TestFixture otherFixture = new(userId: OtherUserId, grantedPermissions: [], dbName: sharedDb);

        await Should.ThrowAsync<UnauthorizedAccessException>(() =>
            otherFixture.Writer.UpdateAsync(
                created.Id,
                new EntityViewUpdateRequest("X", null, null, new JsonObject()),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteAsync_NonOwner_RequiresDeleteAnyPermission()
    {
        string sharedDb = Guid.NewGuid().ToString();
        using TestFixture seedFixture = new(userId: OwnerId, grantedPermissions: [EntityViewPermissions.Create], dbName: sharedDb);
        EntityViewDescriptor created = await seedFixture.Writer.CreateAsync(
            NewCreateRequest(), TestContext.Current.CancellationToken);

        using TestFixture otherFixture = new(userId: OtherUserId, grantedPermissions: [], dbName: sharedDb);

        await Should.ThrowAsync<UnauthorizedAccessException>(() =>
            otherFixture.Writer.DeleteAsync(created.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SetPersonalDefaultAsync_OwnerOnly()
    {
        string sharedDb = Guid.NewGuid().ToString();
        using TestFixture seedFixture = new(userId: OwnerId, grantedPermissions: [EntityViewPermissions.Create], dbName: sharedDb);
        EntityViewDescriptor created = await seedFixture.Writer.CreateAsync(
            NewCreateRequest(), TestContext.Current.CancellationToken);

        using TestFixture otherFixture = new(userId: OtherUserId, grantedPermissions: [EntityViewPermissions.Manage], dbName: sharedDb);

        await Should.ThrowAsync<UnauthorizedAccessException>(() =>
            otherFixture.Writer.SetPersonalDefaultAsync(created.Id, true, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SetPinnedAsync_RequiresManagePermission()
    {
        using TestFixture fixture = new(userId: OwnerId, grantedPermissions: [EntityViewPermissions.Create]);
        EntityViewDescriptor created = await fixture.Writer.CreateAsync(
            NewCreateRequest(), TestContext.Current.CancellationToken);

        await Should.ThrowAsync<UnauthorizedAccessException>(() =>
            fixture.Writer.SetPinnedAsync(created.Id, true, TestContext.Current.CancellationToken));

        fixture.GrantPermission(EntityViewPermissions.Manage);
        EntityViewDescriptor pinned = await fixture.Writer.SetPinnedAsync(
            created.Id, true, TestContext.Current.CancellationToken);
        pinned.IsPinned.ShouldBeTrue();
    }

    private static EntityViewCreateRequest NewCreateRequest() =>
        new(
            EntityName: "Granit.Sample.Item",
            BasedOn: "default",
            Kind: "list",
            Name: "My filter",
            Description: null,
            Icon: null,
            State: new JsonObject());

    private sealed class TestFixture : IDisposable
    {
        public EntityViewDbContext DbContext { get; }
        public EntityViewWriter Writer { get; }
        private readonly HashSet<string> _granted;
        private readonly IPermissionChecker _permissions;

        public TestFixture(
            Guid? userId,
            string[]? grantedPermissions = null,
            string? dbName = null)
        {
            DbContextOptionsBuilder<EntityViewDbContext> options = new DbContextOptionsBuilder<EntityViewDbContext>()
                .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString());

            DbContext = new EntityViewDbContext(options.Options);
            DbContext.Database.EnsureCreated();

            _granted = new HashSet<string>(grantedPermissions ?? [], StringComparer.Ordinal);
            _permissions = Substitute.For<IPermissionChecker>();
            _permissions.IsGrantedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(call => Task.FromResult(_granted.Contains((string)call[0])));

            ICurrentUserService currentUser = Substitute.For<ICurrentUserService>();
            currentUser.UserId.Returns(userId?.ToString());
            currentUser.GetRoles().Returns([]);

            Writer = new EntityViewWriter(DbContext, _permissions, currentUser);
        }

        public void GrantPermission(string permission) => _granted.Add(permission);

        public void Dispose() => DbContext.Dispose();
    }
}
