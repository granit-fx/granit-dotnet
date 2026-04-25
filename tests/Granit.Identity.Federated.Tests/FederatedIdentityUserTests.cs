using Granit.Identity.Federated;
using Granit.Identity.Models;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Tests;

public sealed class FederatedIdentityUserTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var user = new FederatedIdentityUser(
            UserId: "user-1",
            Username: "alice",
            Email: "alice@test.com",
            FirstName: "Alice",
            LastName: "Doe",
            Enabled: true);

        user.UserId.ShouldBe("user-1");
        user.Username.ShouldBe("alice");
        user.Email.ShouldBe("alice@test.com");
        user.FirstName.ShouldBe("Alice");
        user.LastName.ShouldBe("Doe");
        user.Enabled.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_AllowsNullableFields()
    {
        var user = new FederatedIdentityUser(
            UserId: "user-2",
            Username: null,
            Email: null,
            FirstName: null,
            LastName: null,
            Enabled: false);

        user.UserId.ShouldBe("user-2");
        user.Username.ShouldBeNull();
        user.Email.ShouldBeNull();
        user.FirstName.ShouldBeNull();
        user.LastName.ShouldBeNull();
        user.Enabled.ShouldBeFalse();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var user1 = new FederatedIdentityUser("id", "user", "e@test.com", "F", "L", true);
        var user2 = new FederatedIdentityUser("id", "user", "e@test.com", "F", "L", true);

        user1.ShouldBe(user2);
        (user1 == user2).ShouldBeTrue();
    }

    [Fact]
    public void Equality_DifferentId_AreNotEqual()
    {
        var user1 = new FederatedIdentityUser("id-1", "user", "e@test.com", "F", "L", true);
        var user2 = new FederatedIdentityUser("id-2", "user", "e@test.com", "F", "L", true);

        user1.ShouldNotBe(user2);
        (user1 != user2).ShouldBeTrue();
    }

    [Fact]
    public void Equality_DifferentEnabled_AreNotEqual()
    {
        var user1 = new FederatedIdentityUser("id", "user", "e@test.com", "F", "L", true);
        var user2 = new FederatedIdentityUser("id", "user", "e@test.com", "F", "L", false);

        user1.ShouldNotBe(user2);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var original = new FederatedIdentityUser("id", "user", "e@test.com", "F", "L", true);

        FederatedIdentityUser modified = original with { Enabled = false };

        modified.Enabled.ShouldBeFalse();
        modified.UserId.ShouldBe("id");
        original.Enabled.ShouldBeTrue();
    }

    [Fact]
    public void GetHashCode_SameValues_SameHash()
    {
        var user1 = new FederatedIdentityUser("id", "user", "e@test.com", "F", "L", true);
        var user2 = new FederatedIdentityUser("id", "user", "e@test.com", "F", "L", true);

        user1.GetHashCode().ShouldBe(user2.GetHashCode());
    }

    [Fact]
    public void ToString_ContainsTypeName()
    {
        var user = new FederatedIdentityUser("id", "alice", "alice@test.com", "Alice", "Doe", true);

        string str = user.ToString();

        str.ShouldContain("FederatedIdentityUser");
        str.ShouldContain("alice");
    }

    [Fact]
    public void Metadata_DefaultsToNull()
    {
        var user = new FederatedIdentityUser("id", "alice", "alice@test.com", "Alice", "Doe", true);

        user.Metadata.ShouldBeNull();
    }

    [Fact]
    public void Metadata_ViaInterface_NeverNull()
    {
        IIdentityUser user = new FederatedIdentityUser("id", "alice", "alice@test.com", "Alice", "Doe", true);

        user.Metadata.ShouldNotBeNull();
        user.Metadata.ShouldBeEmpty();
    }

    [Fact]
    public void Metadata_WhenProvided_AreAccessible()
    {
        var extras = new Dictionary<string, string> { ["license"] = "MD-12345", ["department"] = "Cardiology" };
        var user = new FederatedIdentityUser("id", "alice", "alice@test.com", "Alice", "Doe", true, extras);

        user.Metadata.ShouldNotBeNull();
        user.Metadata!.Count.ShouldBe(2);
        user.Metadata["license"].ShouldBe("MD-12345");
        user.Metadata["department"].ShouldBe("Cardiology");
    }

    [Fact]
    public void ImplementsIIdentityUser()
    {
        var user = new FederatedIdentityUser("id", "alice", "alice@test.com", "Alice", "Doe", true);

        // Intentionally typed as IIdentityUser to test the interface implementation
#pragma warning disable CA1859
        IIdentityUser identityUser = user;
#pragma warning restore CA1859

        identityUser.UserId.ShouldBe("id");
        identityUser.Username.ShouldBe("alice");
        identityUser.Enabled.ShouldBeTrue();
    }
}
