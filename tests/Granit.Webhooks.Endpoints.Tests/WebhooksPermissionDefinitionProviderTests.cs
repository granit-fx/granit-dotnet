using Granit.Authorization.Abstractions;
using Granit.Localization;
using Granit.Webhooks.Endpoints.Permissions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Endpoints.Tests;

/// <summary>
/// Verifies that <see cref="WebhooksPermissionDefinitionProvider"/> correctly
/// registers the Webhooks permission group and Subscriptions Read/Manage permissions.
/// </summary>
public sealed class WebhooksPermissionDefinitionProviderTests
{
    [Fact]
    public void DefinePermissions_creates_Webhooks_group()
    {
        // Arrange
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        PermissionGroup group = new(WebhooksPermissions.GroupName);
        context.AddGroup(WebhooksPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        WebhooksPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        context.Received(1).AddGroup(WebhooksPermissions.GroupName, Arg.Any<LocalizableString>());
    }

    [Fact]
    public void DefinePermissions_adds_Read_permission()
    {
        // Arrange
        PermissionGroup group = new(WebhooksPermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(WebhooksPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        WebhooksPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        group.Permissions.ShouldContain(p => p.Name == WebhooksPermissions.Subscriptions.Read);
    }

    [Fact]
    public void DefinePermissions_adds_Manage_permission()
    {
        // Arrange
        PermissionGroup group = new(WebhooksPermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(WebhooksPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        WebhooksPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        group.Permissions.ShouldContain(p => p.Name == WebhooksPermissions.Subscriptions.Manage);
    }

    [Fact]
    public void DefinePermissions_registers_exactly_two_permissions()
    {
        // Arrange
        PermissionGroup group = new(WebhooksPermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(WebhooksPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        WebhooksPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        group.Permissions.Count.ShouldBe(2);
    }
}
