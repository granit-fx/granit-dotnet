using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Tests;

public sealed class FederatedIdentityUserTests
{
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
