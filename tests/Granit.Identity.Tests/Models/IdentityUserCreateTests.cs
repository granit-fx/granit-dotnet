using Granit.Identity.Models;
using Shouldly;
using Xunit;

namespace Granit.Identity.Tests.Models;

public sealed class IdentityUserCreateTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var create = new IdentityUserCreate(
            Username: "alice",
            Email: "alice@test.com",
            FirstName: "Alice",
            LastName: "Doe",
            Enabled: true,
            TemporaryPassword: "Temp123!");

        create.Username.ShouldBe("alice");
        create.Email.ShouldBe("alice@test.com");
        create.FirstName.ShouldBe("Alice");
        create.LastName.ShouldBe("Doe");
        create.Enabled.ShouldBeTrue();
        create.TemporaryPassword.ShouldBe("Temp123!");
    }

    [Fact]
    public void Constructor_DefaultsOptionalFields()
    {
        var create = new IdentityUserCreate(
            Username: "bob",
            Email: "bob@test.com");

        create.FirstName.ShouldBeNull();
        create.LastName.ShouldBeNull();
        create.Enabled.ShouldBeTrue();
        create.TemporaryPassword.ShouldBeNull();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var create1 = new IdentityUserCreate("alice", "alice@test.com", "Alice", "Doe", true, "pass");
        var create2 = new IdentityUserCreate("alice", "alice@test.com", "Alice", "Doe", true, "pass");

        create1.ShouldBe(create2);
    }

    [Fact]
    public void Equality_DifferentUsername_AreNotEqual()
    {
        var create1 = new IdentityUserCreate("alice", "alice@test.com");
        var create2 = new IdentityUserCreate("bob", "alice@test.com");

        create1.ShouldNotBe(create2);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var original = new IdentityUserCreate("alice", "alice@test.com");

        IdentityUserCreate modified = original with { Enabled = false };

        modified.Enabled.ShouldBeFalse();
        original.Enabled.ShouldBeTrue();
    }

    [Fact]
    public void ToString_ContainsTypeName()
    {
        var create = new IdentityUserCreate("alice", "alice@test.com");

        string str = create.ToString();

        str.ShouldContain("IdentityUserCreate");
        str.ShouldContain("alice");
    }
}
