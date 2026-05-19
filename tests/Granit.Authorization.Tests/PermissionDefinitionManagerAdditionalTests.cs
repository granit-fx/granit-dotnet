using Granit.Authorization.Services;
using Granit.Localization;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Tests;

public sealed class PermissionDefinitionManagerAdditionalTests
{
    [Fact]
    public void Find_ExistingPermission_ReturnsDefinition()
    {
        IPermissionDefinitionProvider[] providers = [new TestProvider()];
        PermissionDefinitionManager manager = new(providers);

        PermissionDefinition? result = manager.Find("Test.Read");

        result.ShouldNotBeNull();
        result!.Name.ShouldBe("Test.Read");
        result.GroupName.ShouldBe("Test");
    }

    [Fact]
    public void Find_NonExistingPermission_ReturnsNull()
    {
        IPermissionDefinitionProvider[] providers = [new TestProvider()];
        PermissionDefinitionManager manager = new(providers);

        PermissionDefinition? result = manager.Find("NonExistent.Permission");

        result.ShouldBeNull();
    }

    [Fact]
    public void Constructor_NoProviders_EmptyPermissions()
    {
        PermissionDefinitionManager manager = new([]);

        manager.GetAll().ShouldBeEmpty();
        manager.GetGroups().ShouldBeEmpty();
    }

    [Fact]
    public void GetAll_ReturnsAllPermissionsAsReadOnlyList()
    {
        IPermissionDefinitionProvider[] providers = [new TestProvider()];
        PermissionDefinitionManager manager = new(providers);

        IReadOnlyList<PermissionDefinition> all = manager.GetAll();

        all.ShouldBeAssignableTo<IReadOnlyList<PermissionDefinition>>();
        all.Count.ShouldBe(2);
    }

    [Fact]
    public void GetGroups_ReturnsGroupsAsReadOnlyList()
    {
        IPermissionDefinitionProvider[] providers = [new TestProvider()];
        PermissionDefinitionManager manager = new(providers);

        IReadOnlyList<PermissionGroup> groups = manager.GetGroups();

        groups.ShouldBeAssignableTo<IReadOnlyList<PermissionGroup>>();
        groups.Count.ShouldBe(1);
    }

    [Fact]
    public void Find_WithDisplayName_ReturnsDisplayName()
    {
        IPermissionDefinitionProvider[] providers = [new TestProvider()];
        PermissionDefinitionManager manager = new(providers);

        PermissionDefinition? result = manager.Find("Test.Read");

        result.ShouldNotBeNull();
        result!.DisplayName.ShouldNotBeNull();
    }

    private sealed class TestProvider : IPermissionDefinitionProvider
    {
        public void DefinePermissions(IPermissionDefinitionContext context)
        {
            PermissionGroup group = context.AddGroup("Test", LocalizableString.Fixed("Test Group"));
            group.AddPermission("Test.Read", LocalizableString.Fixed("Read test"));
            group.AddPermission("Test.Write", LocalizableString.Fixed("Write test"));
        }
    }
}
