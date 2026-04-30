// =============================================================================
// Tests - EntityViewSharedWith value object
// =============================================================================

using Shouldly;
using Xunit;

namespace Granit.Entities.Views.Abstractions.Tests;

public sealed class EntityViewSharedWithTests
{
    [Fact]
    public void Empty_HasZeroRolesAndZeroUsers()
    {
        EntityViewSharedWith.Empty.Roles.ShouldBeEmpty();
        EntityViewSharedWith.Empty.Users.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_AcceptsRolesAndUsers()
    {
        EntityViewSharedWith audience = new(["Admin", "Manager"], [Guid.NewGuid(), Guid.NewGuid()]);

        audience.Roles.Count.ShouldBe(2);
        audience.Users.Count.ShouldBe(2);
    }

    [Fact]
    public void Equality_ComparesByValue_NotReference()
    {
        var u = Guid.NewGuid();
        EntityViewSharedWith a = new(["Admin"], [u]);
        EntityViewSharedWith b = new(["Admin"], [u]);

        // record equality works on the readonly references — same content == equal
        a.Roles.SequenceEqual(b.Roles).ShouldBeTrue();
        a.Users.SequenceEqual(b.Users).ShouldBeTrue();
    }
}
