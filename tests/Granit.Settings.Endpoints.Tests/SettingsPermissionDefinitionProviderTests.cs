using Granit.Authorization.Abstractions;
using Granit.Core.Localization;
using Granit.Settings.Endpoints.Permissions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Settings.Endpoints.Tests;

public sealed class SettingsPermissionDefinitionProviderTests
{
    [Fact]
    public void DefinePermissions_CreatesSettingsGroup()
    {
        SettingsPermissionDefinitionProvider provider = new();
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        PermissionGroup group = new(SettingsPermissions.GroupName);
        context.AddGroup(SettingsPermissions.GroupName, Arg.Any<LocalizableString?>()).Returns(group);

        provider.DefinePermissions(context);

        context.Received(1).AddGroup(
            SettingsPermissions.GroupName,
            Arg.Any<LocalizableString?>());
    }

    [Fact]
    public void DefinePermissions_RegistersFourPermissions()
    {
        SettingsPermissionDefinitionProvider provider = new();
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        PermissionGroup group = new(SettingsPermissions.GroupName);
        context.AddGroup(Arg.Any<string>(), Arg.Any<LocalizableString?>()).Returns(group);

        provider.DefinePermissions(context);

        group.Permissions.Count.ShouldBe(4);
    }

    [Fact]
    public void DefinePermissions_RegistersGlobalReadPermission()
    {
        SettingsPermissionDefinitionProvider provider = new();
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        PermissionGroup group = new(SettingsPermissions.GroupName);
        context.AddGroup(Arg.Any<string>(), Arg.Any<LocalizableString?>()).Returns(group);

        provider.DefinePermissions(context);

        group.Permissions.ShouldContain(p => p.Name == SettingsPermissions.Global.Read);
    }

    [Fact]
    public void DefinePermissions_RegistersGlobalManagePermission()
    {
        SettingsPermissionDefinitionProvider provider = new();
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        PermissionGroup group = new(SettingsPermissions.GroupName);
        context.AddGroup(Arg.Any<string>(), Arg.Any<LocalizableString?>()).Returns(group);

        provider.DefinePermissions(context);

        group.Permissions.ShouldContain(p => p.Name == SettingsPermissions.Global.Manage);
    }

    [Fact]
    public void DefinePermissions_RegistersTenantReadPermission()
    {
        SettingsPermissionDefinitionProvider provider = new();
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        PermissionGroup group = new(SettingsPermissions.GroupName);
        context.AddGroup(Arg.Any<string>(), Arg.Any<LocalizableString?>()).Returns(group);

        provider.DefinePermissions(context);

        group.Permissions.ShouldContain(p => p.Name == SettingsPermissions.Tenant.Read);
    }

    [Fact]
    public void DefinePermissions_RegistersTenantManagePermission()
    {
        SettingsPermissionDefinitionProvider provider = new();
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        PermissionGroup group = new(SettingsPermissions.GroupName);
        context.AddGroup(Arg.Any<string>(), Arg.Any<LocalizableString?>()).Returns(group);

        provider.DefinePermissions(context);

        group.Permissions.ShouldContain(p => p.Name == SettingsPermissions.Tenant.Manage);
    }
}
