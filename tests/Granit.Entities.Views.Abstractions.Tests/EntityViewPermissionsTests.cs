// =============================================================================
// Tests - EntityViewPermissions constants (closed enum, ADR-047 §6)
// =============================================================================

using Shouldly;
using Xunit;

namespace Granit.Entities.Views.Abstractions.Tests;

public sealed class EntityViewPermissionsTests
{
    [Fact]
    public void GroupName_IsEntitiesViews()
    {
        EntityViewPermissions.GroupName.ShouldBe("Entities.Views");
    }

    [Theory]
    [InlineData(nameof(EntityViewPermissions.Read), "Entities.Views.Read")]
    [InlineData(nameof(EntityViewPermissions.Create), "Entities.Views.Create")]
    [InlineData(nameof(EntityViewPermissions.Share), "Entities.Views.Share")]
    [InlineData(nameof(EntityViewPermissions.Manage), "Entities.Views.Manage")]
    [InlineData(nameof(EntityViewPermissions.DeleteAny), "Entities.Views.DeleteAny")]
    public void EachPermission_FollowsConvention(string fieldName, string expectedValue)
    {
        System.Reflection.FieldInfo field = typeof(EntityViewPermissions)
            .GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!;
        field.GetRawConstantValue().ShouldBe(expectedValue);
    }

    [Fact]
    public void GranitEntitiesViewsAbstractionsModule_IsAGranitModule()
    {
        typeof(Granit.Modularity.GranitModule)
            .IsAssignableFrom(typeof(GranitEntitiesViewsAbstractionsModule))
            .ShouldBeTrue();
    }
}
