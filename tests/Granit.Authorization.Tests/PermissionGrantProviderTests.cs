using Granit.Authorization.Services;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Tests;

/// <summary>
/// Covers the key-extraction behavior of the three built-in grant providers.
/// </summary>
public sealed class PermissionGrantProviderTests
{
    [Fact]
    public void User_NameIsU()
        => new UserPermissionGrantProvider().Name.ShouldBe("U");

    [Fact]
    public void Role_NameIsR()
        => new RolePermissionGrantProvider().Name.ShouldBe("R");

    [Fact]
    public void Client_NameIsC()
        => new ClientPermissionGrantProvider().Name.ShouldBe("C");

    // --- User provider ---

    [Fact]
    public void User_NoUserId_ReturnsEmpty()
    {
        IReadOnlyList<string> keys = new UserPermissionGrantProvider()
            .GetProviderKeys(new PermissionGrantLookupContext(UserId: null, Roles: [], ClientId: null));

        keys.ShouldBeEmpty();
    }

    [Fact]
    public void User_HasUserId_ReturnsSingleton()
    {
        IReadOnlyList<string> keys = new UserPermissionGrantProvider()
            .GetProviderKeys(new PermissionGrantLookupContext("alice", Roles: ["editor"], ClientId: "spa"));

        keys.ShouldBe(["alice"]);
    }

    // --- Role provider ---

    [Fact]
    public void Role_NoRoles_ReturnsEmpty()
    {
        IReadOnlyList<string> keys = new RolePermissionGrantProvider()
            .GetProviderKeys(new PermissionGrantLookupContext("alice", Roles: [], ClientId: null));

        keys.ShouldBeEmpty();
    }

    [Fact]
    public void Role_MultipleRoles_ReturnsAll()
    {
        IReadOnlyList<string> keys = new RolePermissionGrantProvider()
            .GetProviderKeys(new PermissionGrantLookupContext("alice", Roles: ["editor", "reader"], ClientId: null));

        keys.ShouldBe(["editor", "reader"]);
    }

    // --- Client provider ---

    [Fact]
    public void Client_NoClientId_ReturnsEmpty()
    {
        IReadOnlyList<string> keys = new ClientPermissionGrantProvider()
            .GetProviderKeys(new PermissionGrantLookupContext("alice", Roles: [], ClientId: null));

        keys.ShouldBeEmpty();
    }

    [Fact]
    public void Client_HasClientId_ReturnsSingleton()
    {
        IReadOnlyList<string> keys = new ClientPermissionGrantProvider()
            .GetProviderKeys(new PermissionGrantLookupContext(null, Roles: [], ClientId: "m2m-worker"));

        keys.ShouldBe(["m2m-worker"]);
    }
}
