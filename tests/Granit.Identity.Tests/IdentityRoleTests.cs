using Granit.Identity.Models;
using Shouldly;
using Xunit;

namespace Granit.Identity.Tests;

public sealed class IdentityRoleTests
{
    [Fact]
    public void Constructor_AllowsNullDescription()
    {
        var role = new IdentityRole(
            Id: "role-2",
            Name: "admin",
            Description: null);

        role.Id.ShouldBe("role-2");
        role.Name.ShouldBe("admin");
        role.Description.ShouldBeNull();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var role1 = new IdentityRole("id", "admin", "Administrator");
        var role2 = new IdentityRole("id", "admin", "Administrator");

        role1.ShouldBe(role2);
        (role1 == role2).ShouldBeTrue();
    }

    [Fact]
    public void Equality_DifferentId_AreNotEqual()
    {
        var role1 = new IdentityRole("id-1", "admin", "Administrator");
        var role2 = new IdentityRole("id-2", "admin", "Administrator");

        role1.ShouldNotBe(role2);
        (role1 != role2).ShouldBeTrue();
    }

    [Fact]
    public void Equality_DifferentName_AreNotEqual()
    {
        var role1 = new IdentityRole("id", "admin", "Administrator");
        var role2 = new IdentityRole("id", "editor", "Administrator");

        role1.ShouldNotBe(role2);
    }

    [Fact]
    public void Equality_DifferentDescription_AreNotEqual()
    {
        var role1 = new IdentityRole("id", "admin", "Administrator");
        var role2 = new IdentityRole("id", "admin", "Different");

        role1.ShouldNotBe(role2);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var original = new IdentityRole("id", "admin", "Administrator");

        IdentityRole modified = original with { Description = "New description" };

        modified.Description.ShouldBe("New description");
        modified.Id.ShouldBe("id");
        original.Description.ShouldBe("Administrator");
    }

    [Fact]
    public void GetHashCode_SameValues_SameHash()
    {
        var role1 = new IdentityRole("id", "admin", "Administrator");
        var role2 = new IdentityRole("id", "admin", "Administrator");

        role1.GetHashCode().ShouldBe(role2.GetHashCode());
    }

    [Fact]
    public void ToString_ContainsTypeName()
    {
        var role = new IdentityRole("id", "editor", "Content editor");

        string str = role.ToString();

        str.ShouldContain("IdentityRole");
        str.ShouldContain("editor");
    }
}
