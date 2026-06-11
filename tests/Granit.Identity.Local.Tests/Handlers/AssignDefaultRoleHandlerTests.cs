using Granit.Identity.Local.Events;
using Granit.Identity.Local.Handlers;
using Granit.Identity.Models;
using Granit.MultiTenancy;
using Granit.Settings.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.Tests.Handlers;

public sealed class AssignDefaultRoleHandlerTests
{
    private readonly ISettingProvider _settings = Substitute.For<ISettingProvider>();
    private readonly IIdentityRoleManager _roles = Substitute.For<IIdentityRoleManager>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();

    private Task InvokeAsync(Guid userId, Guid? tenantId = null) =>
        AssignDefaultRoleHandler.HandleAsync(
            new UserRegisteredEto(userId, tenantId),
            _settings, _roles, _currentTenant,
            NullLogger<AssignDefaultRoleHandler>.Instance,
            TestContext.Current.CancellationToken);

    private void ConfigureRole(string? role) =>
        _settings.GetOrNullAsync(IdentityLocalSettingNames.DefaultUserRole, Arg.Any<CancellationToken>())
            .Returns(role);

    [Fact]
    public async Task SettingEmpty_DoesNotAssignAnyRole()
    {
        ConfigureRole(null);

        await InvokeAsync(Guid.NewGuid());

        await _roles.DidNotReceive().AssignRoleAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SettingSet_AssignsConfiguredRole()
    {
        var userId = Guid.NewGuid();
        ConfigureRole("User");
        _roles.GetUserRolesAsync(userId.ToString(), Arg.Any<CancellationToken>())
            .Returns([]);

        await InvokeAsync(userId);

        await _roles.Received(1).AssignRoleAsync(userId.ToString(), "User", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AlreadyInRole_IsIdempotent_DoesNotReassign()
    {
        var userId = Guid.NewGuid();
        ConfigureRole("User");
        _roles.GetUserRolesAsync(userId.ToString(), Arg.Any<CancellationToken>())
            .Returns([new IdentityRole("role-id", "user", null)]); // case-insensitive match

        await InvokeAsync(userId);

        await _roles.DidNotReceive().AssignRoleAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RoleDoesNotExist_SwallowsAndDoesNotThrow()
    {
        var userId = Guid.NewGuid();
        ConfigureRole("Ghost");
        _roles.GetUserRolesAsync(userId.ToString(), Arg.Any<CancellationToken>())
            .Returns([]);
        _roles.AssignRoleAsync(userId.ToString(), "Ghost", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Role assignment failed: Role Ghost does not exist."));

        await Should.NotThrowAsync(() => InvokeAsync(userId));
    }

    [Fact]
    public async Task EntersTenantScope_OfTheRegisteredUser()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        ConfigureRole("User");
        _roles.GetUserRolesAsync(userId.ToString(), Arg.Any<CancellationToken>())
            .Returns([]);

        await InvokeAsync(userId, tenantId);

        _currentTenant.Received(1).Change(tenantId);
    }
}
