using System.Security.Claims;
using Granit.Authorization.Caching;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Tests.Caching;

public sealed class PermissionsCacheKeyHashTests
{
    private static ClaimsPrincipal Principal(string? sub, params (string Type, string Value)[] claims)
    {
        List<Claim> list = [.. claims.Select(c => new Claim(c.Type, c.Value))];
        if (sub is not null)
        {
            list.Add(new Claim(ClaimTypes.NameIdentifier, sub));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(list, authenticationType: "test"));
    }

    [Fact]
    public void Compute_NullUser_Throws()
        => Should.Throw<ArgumentNullException>(() => PermissionsCacheKeyHash.Compute(null!));

    [Fact]
    public void Compute_ShapeIs16LowercaseHexChars()
    {
        string hash = PermissionsCacheKeyHash.Compute(Principal("alice"));

        hash.Length.ShouldBe(16);
        hash.ShouldMatch("^[0-9a-f]{16}$");
    }

    [Fact]
    public void Compute_SamePrincipal_IsDeterministic()
    {
        ClaimsPrincipal a = Principal("alice", (ClaimTypes.Role, "admin"), ("permission", "Workspaces.Read"));
        ClaimsPrincipal b = Principal("alice", (ClaimTypes.Role, "admin"), ("permission", "Workspaces.Read"));

        PermissionsCacheKeyHash.Compute(a).ShouldBe(PermissionsCacheKeyHash.Compute(b));
    }

    [Fact]
    public void Compute_IsInsensitiveToClaimOrder()
    {
        ClaimsPrincipal a = Principal(
            "alice",
            (ClaimTypes.Role, "admin"),
            ("permission", "Workspaces.Read"),
            ("permission", "Entities.Manage"));
        ClaimsPrincipal b = Principal(
            "alice",
            ("permission", "Entities.Manage"),
            (ClaimTypes.Role, "admin"),
            ("permission", "Workspaces.Read"));

        PermissionsCacheKeyHash.Compute(a).ShouldBe(PermissionsCacheKeyHash.Compute(b));
    }

    [Fact]
    public void Compute_DifferentClaimSets_ProduceDifferentHashes()
    {
        ClaimsPrincipal a = Principal("alice", (ClaimTypes.Role, "admin"));
        ClaimsPrincipal b = Principal("alice", (ClaimTypes.Role, "viewer"));

        PermissionsCacheKeyHash.Compute(a).ShouldNotBe(PermissionsCacheKeyHash.Compute(b));
    }

    [Fact]
    public void Compute_SameClaimsButDifferentSubject_ProduceDifferentHashes()
    {
        ClaimsPrincipal a = Principal("alice", ("permission", "Workspaces.Read"));
        ClaimsPrincipal b = Principal("bob", ("permission", "Workspaces.Read"));

        PermissionsCacheKeyHash.Compute(a).ShouldNotBe(PermissionsCacheKeyHash.Compute(b));
    }

    [Fact]
    public void Compute_AnonymousPrincipal_HashesAsAnonBucket()
    {
        // Two anonymous principals with identical claim sets must collide — that's the bucket.
        ClaimsPrincipal a = Principal(sub: null, (ClaimTypes.Role, "guest"));
        ClaimsPrincipal b = Principal(sub: null, (ClaimTypes.Role, "guest"));

        PermissionsCacheKeyHash.Compute(a).ShouldBe(PermissionsCacheKeyHash.Compute(b));

        // And differ from an identified principal with the same role.
        ClaimsPrincipal identified = Principal("alice", (ClaimTypes.Role, "guest"));
        PermissionsCacheKeyHash.Compute(a).ShouldNotBe(PermissionsCacheKeyHash.Compute(identified));
    }

    [Fact]
    public void Compute_IgnoresNonRbacClaimTypes()
    {
        // name, email, etc. must not contribute to the partition key — only role/permission do.
        ClaimsPrincipal lean = Principal("alice", (ClaimTypes.Role, "admin"));
        ClaimsPrincipal noisy = Principal(
            "alice",
            (ClaimTypes.Role, "admin"),
            (ClaimTypes.Name, "Alice Example"),
            (ClaimTypes.Email, "alice@example.test"),
            ("custom", "ignored"));

        PermissionsCacheKeyHash.Compute(lean).ShouldBe(PermissionsCacheKeyHash.Compute(noisy));
    }

    [Fact]
    public void Compute_AcceptsShortRoleClaimType()
    {
        // JWT-issued principals often use the short "role" type instead of ClaimTypes.Role.
        ClaimsPrincipal longForm = Principal("alice", (ClaimTypes.Role, "admin"));
        ClaimsPrincipal shortForm = Principal("alice", ("role", "admin"));

        // The two are distinct claim types — they MUST hash differently (we want the type
        // included in the digest so an issuer change is visible to the cache partition).
        PermissionsCacheKeyHash.Compute(longForm).ShouldNotBe(PermissionsCacheKeyHash.Compute(shortForm));
    }

    [Fact]
    public void Compute_UsesJwtSubClaim_WhenNameIdentifierMissing()
    {
        ClaimsPrincipal viaNameId = Principal("alice", (ClaimTypes.Role, "admin"));
        ClaimsPrincipal viaSub = new(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, "admin"), new Claim("sub", "alice")],
            authenticationType: "test"));

        PermissionsCacheKeyHash.Compute(viaNameId).ShouldBe(PermissionsCacheKeyHash.Compute(viaSub));
    }
}
