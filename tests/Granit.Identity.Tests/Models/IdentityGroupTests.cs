using Granit.Identity.Models;
using Shouldly;
using Xunit;

namespace Granit.Identity.Tests.Models;

public sealed class IdentityGroupTests
{
    [Fact]
    public void Constructor_AllowsNullPath()
    {
        var group = new IdentityGroup(
            Id: "root-1",
            Name: "Root Group",
            Path: null,
            SubGroups: []);

        group.Path.ShouldBeNull();
        group.SubGroups.ShouldBeEmpty();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        List<IdentityGroup> subs = [];
        var group1 = new IdentityGroup("id", "name", "/path", subs);
        var group2 = new IdentityGroup("id", "name", "/path", subs);

        group1.ShouldBe(group2);
    }

    [Fact]
    public void Equality_DifferentId_AreNotEqual()
    {
        List<IdentityGroup> subs = [];
        var group1 = new IdentityGroup("id-1", "name", "/path", subs);
        var group2 = new IdentityGroup("id-2", "name", "/path", subs);

        group1.ShouldNotBe(group2);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var original = new IdentityGroup("id", "Original", "/path", []);

        IdentityGroup modified = original with { Name = "Modified" };

        modified.Name.ShouldBe("Modified");
        original.Name.ShouldBe("Original");
    }

    [Fact]
    public void ToString_ContainsTypeName()
    {
        var group = new IdentityGroup("id", "Admins", "/admins", []);

        string str = group.ToString();

        str.ShouldContain("IdentityGroup");
        str.ShouldContain("Admins");
    }

    [Fact]
    public void NestedSubGroups_AreAccessible()
    {
        var grandchild = new IdentityGroup("gc-1", "Grandchild", "/root/child/grandchild", []);
        var child = new IdentityGroup("c-1", "Child", "/root/child", [grandchild]);
        var root = new IdentityGroup("r-1", "Root", "/root", [child]);

        root.SubGroups[0].SubGroups[0].Name.ShouldBe("Grandchild");
    }
}
