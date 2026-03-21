using Granit.Identity.Models;
using Shouldly;
using Xunit;

namespace Granit.Identity.Tests.Models;

public sealed class IdentityUserUpdateTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        Dictionary<string, string?> attrs = new() { ["key"] = "value", ["remove"] = null };

        var update = new IdentityUserUpdate(
            Email: "new@test.com",
            FirstName: "New",
            LastName: "Name",
            Attributes: attrs);

        update.Email.ShouldBe("new@test.com");
        update.FirstName.ShouldBe("New");
        update.LastName.ShouldBe("Name");
        update.Attributes.ShouldNotBeNull();
        update.Attributes!.Count.ShouldBe(2);
    }

    [Fact]
    public void Constructor_DefaultsAllToNull()
    {
        var update = new IdentityUserUpdate();

        update.Email.ShouldBeNull();
        update.FirstName.ShouldBeNull();
        update.LastName.ShouldBeNull();
        update.Attributes.ShouldBeNull();
    }

    [Fact]
    public void Constructor_PartialUpdate_OnlyEmailSet()
    {
        var update = new IdentityUserUpdate(Email: "updated@test.com");

        update.Email.ShouldBe("updated@test.com");
        update.FirstName.ShouldBeNull();
        update.LastName.ShouldBeNull();
        update.Attributes.ShouldBeNull();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var update1 = new IdentityUserUpdate(Email: "e@test.com", FirstName: "F");
        var update2 = new IdentityUserUpdate(Email: "e@test.com", FirstName: "F");

        update1.ShouldBe(update2);
    }

    [Fact]
    public void Equality_DifferentEmail_AreNotEqual()
    {
        var update1 = new IdentityUserUpdate(Email: "a@test.com");
        var update2 = new IdentityUserUpdate(Email: "b@test.com");

        update1.ShouldNotBe(update2);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var original = new IdentityUserUpdate(Email: "old@test.com");

        IdentityUserUpdate modified = original with { Email = "new@test.com" };

        modified.Email.ShouldBe("new@test.com");
        original.Email.ShouldBe("old@test.com");
    }

    [Fact]
    public void Attributes_NullValueIndicatesRemoval()
    {
        Dictionary<string, string?> attrs = new() { ["remove-me"] = null };
        var update = new IdentityUserUpdate(Attributes: attrs);

        update.Attributes!["remove-me"].ShouldBeNull();
    }

    [Fact]
    public void ToString_ContainsTypeName()
    {
        var update = new IdentityUserUpdate(Email: "test@test.com");

        string str = update.ToString();

        str.ShouldContain("IdentityUserUpdate");
    }
}
