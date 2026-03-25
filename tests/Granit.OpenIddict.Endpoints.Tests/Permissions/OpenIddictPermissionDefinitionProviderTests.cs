using Granit.Authorization.Abstractions;
using Granit.Localization;
using Granit.OpenIddict.Endpoints.Permissions;
using Granit.OpenIddict.Permissions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Endpoints.Tests.Permissions;

public sealed class OpenIddictPermissionDefinitionProviderTests
{
    [Fact]
    public void DefinePermissions_RegistersOpenIddictGroup()
    {
        OpenIddictPermissionDefinitionProvider provider = new();
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        var group = new PermissionGroup(OpenIddictPermissions.GroupName, null);
        context.AddGroup(Arg.Any<string>(), Arg.Any<LocalizableString?>()).Returns(group);

        provider.DefinePermissions(context);

        context.Received(1).AddGroup(
            OpenIddictPermissions.GroupName,
            Arg.Any<LocalizableString?>());
    }

    [Fact]
    public void DefinePermissions_RegistersAllPermissions()
    {
        OpenIddictPermissionDefinitionProvider provider = new();
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        var group = new PermissionGroup(OpenIddictPermissions.GroupName, null);
        context.AddGroup(Arg.Any<string>(), Arg.Any<LocalizableString?>()).Returns(group);

        provider.DefinePermissions(context);

        // 1 Users (Impersonate) + 5 Applications + 4 Scopes + 2 Authorizations = 12
        group.Permissions.Count.ShouldBe(12);
    }
}
