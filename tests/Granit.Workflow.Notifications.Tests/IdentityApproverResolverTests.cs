using Granit.Authorization.Abstractions;
using Granit.Core.MultiTenancy;
using Granit.Identity;
using Granit.Tests.Shared;
using Granit.Workflow.Notifications.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Workflow.Notifications.Tests;

public sealed class IdentityApproverResolverTests
{
    private readonly IPermissionManagerReader _permissionManagerReader = Substitute.For<IPermissionManagerReader>();
    private readonly IIdentityProvider _identityProvider = Substitute.For<IIdentityProvider>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly IdentityApproverResolver _resolver;

    public IdentityApproverResolverTests()
    {
        _currentTenant.IsAvailable.Returns(false);

        _resolver = new IdentityApproverResolver(
            _permissionManagerReader,
            _identityProvider,
            _currentTenant,
            NullLogger<IdentityApproverResolver>.Instance);
    }

    [Fact]
    public async Task ResolveApproversAsync_WithRolesAndUsers_ReturnsUserIds()
    {
        _permissionManagerReader.GetGrantedRolesAsync("workflow.publish", null, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<string>)["editor"]);

        _identityProvider.GetRoleMembersAsync("editor", Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<IIdentityUser>)
            [
                new FakeIdentityUser("user-1", "alice", "alice@test.com", "Alice", "Doe", true),
                new FakeIdentityUser("user-2", "bob", null, null, null, true),
            ]);

        IReadOnlyList<string> result = await _resolver.ResolveApproversAsync(
            "workflow.publish", TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result.ShouldContain("user-1");
        result.ShouldContain("user-2");
    }

    [Fact]
    public async Task ResolveApproversAsync_NoRolesGranted_ReturnsEmptyList()
    {
        _permissionManagerReader.GetGrantedRolesAsync("workflow.publish", null, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<string>)[]);

        IReadOnlyList<string> result = await _resolver.ResolveApproversAsync(
            "workflow.publish", TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ResolveApproversAsync_RoleWithNoUsers_ReturnsEmptyList()
    {
        _permissionManagerReader.GetGrantedRolesAsync("workflow.publish", null, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<string>)["editor"]);

        _identityProvider.GetRoleMembersAsync("editor", Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<IIdentityUser>)[]);

        IReadOnlyList<string> result = await _resolver.ResolveApproversAsync(
            "workflow.publish", TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ResolveApproversAsync_DuplicateUsersAcrossRoles_ReturnsDeduplicatedList()
    {
        _permissionManagerReader.GetGrantedRolesAsync("workflow.publish", null, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<string>)["editor", "admin"]);

        _identityProvider.GetRoleMembersAsync("editor", Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<IIdentityUser>)
            [
                new FakeIdentityUser("shared-user", "shared", null, null, null, true),
                new FakeIdentityUser("editor-user", "editor", null, null, null, true),
            ]);

        _identityProvider.GetRoleMembersAsync("admin", Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<IIdentityUser>)
            [
                new FakeIdentityUser("shared-user", "shared", null, null, null, true),
                new FakeIdentityUser("admin-user", "admin", null, null, null, true),
            ]);

        IReadOnlyList<string> result = await _resolver.ResolveApproversAsync(
            "workflow.publish", TestContext.Current.CancellationToken);

        result.Count.ShouldBe(3);
        result.ShouldContain("shared-user");
        result.ShouldContain("editor-user");
        result.ShouldContain("admin-user");
    }

    [Fact]
    public async Task ResolveApproversAsync_WithTenant_PassesTenantId()
    {
        var tenantId = Guid.NewGuid();
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(tenantId);

        _permissionManagerReader.GetGrantedRolesAsync("workflow.publish", tenantId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<string>)["editor"]);

        _identityProvider.GetRoleMembersAsync("editor", Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<IIdentityUser>)
            [
                new FakeIdentityUser("user-1", "alice", null, null, null, true),
            ]);

        IReadOnlyList<string> result = await _resolver.ResolveApproversAsync(
            "workflow.publish", TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        await _permissionManagerReader.Received(1).GetGrantedRolesAsync(
            "workflow.publish", tenantId, Arg.Any<CancellationToken>());
    }
}
