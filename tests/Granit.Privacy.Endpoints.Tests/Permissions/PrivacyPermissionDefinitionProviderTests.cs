using Granit.Authorization;
using Granit.Localization;
using Granit.Privacy.Endpoints.Permissions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Endpoints.Tests.Permissions;

public sealed class PrivacyPermissionDefinitionProviderTests
{
    [Fact]
    public void DefinePermissions_CreatesPrivacyGroup()
    {
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        PermissionGroup group = new(PrivacyPermissions.GroupName);
        context.AddGroup(PrivacyPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        PrivacyPermissionDefinitionProvider provider = new();
        provider.DefinePermissions(context);

        context.Received(1).AddGroup(PrivacyPermissions.GroupName, Arg.Any<LocalizableString>());
    }

    [Theory]
    [InlineData("Privacy.Exports.Execute")]
    [InlineData("Privacy.Exports.OnBehalfOf")]
    [InlineData("Privacy.Deletions.Execute")]
    [InlineData("Privacy.Agreements.Read")]
    [InlineData("Privacy.Agreements.Create")]
    public void DefinePermissions_RegistersPermission(string permissionName)
    {
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        PermissionGroup group = new(PrivacyPermissions.GroupName);
        context.AddGroup(PrivacyPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        PrivacyPermissionDefinitionProvider provider = new();
        provider.DefinePermissions(context);

        group.Permissions.ShouldContain(p => p.Name == permissionName);
    }

    [Fact]
    public void DefinePermissions_RegistersExactlySixPermissions()
    {
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        PermissionGroup group = new(PrivacyPermissions.GroupName);
        context.AddGroup(PrivacyPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        PrivacyPermissionDefinitionProvider provider = new();
        provider.DefinePermissions(context);

        group.Permissions.Count.ShouldBe(6);
    }
}
