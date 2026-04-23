using Granit.Authorization;
using Granit.DataLookup.Endpoints.Permissions;
using Granit.Localization;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.DataLookup.Endpoints.Tests;

public sealed class DataLookupPermissionDefinitionProviderTests
{
    [Fact]
    public void DefinePermissions_creates_DataLookup_group()
    {
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        PermissionGroup group = new(DataLookupPermissions.GroupName);
        context.AddGroup(DataLookupPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        DataLookupPermissionDefinitionProvider provider = new();

        provider.DefinePermissions(context);

        context.Received(1).AddGroup(DataLookupPermissions.GroupName, Arg.Any<LocalizableString>());
    }

    [Fact]
    public void DefinePermissions_adds_Lookups_Read_permission()
    {
        PermissionGroup group = new(DataLookupPermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(DataLookupPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        DataLookupPermissionDefinitionProvider provider = new();
        provider.DefinePermissions(context);

        group.Permissions.ShouldContain(p => p.Name == DataLookupPermissions.Lookups.Read);
    }

    [Fact]
    public void DefinePermissions_registers_exactly_one_permission()
    {
        PermissionGroup group = new(DataLookupPermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(DataLookupPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        DataLookupPermissionDefinitionProvider provider = new();
        provider.DefinePermissions(context);

        group.Permissions.Count.ShouldBe(1);
    }
}

public sealed class DataLookupPermissionsTests
{
    [Fact]
    public void Read_constant_follows_Group_Resource_Action_format() =>
        DataLookupPermissions.Lookups.Read.ShouldBe("DataLookup.Lookups.Read");

    [Fact]
    public void GroupName_is_DataLookup() =>
        DataLookupPermissions.GroupName.ShouldBe("DataLookup");
}
