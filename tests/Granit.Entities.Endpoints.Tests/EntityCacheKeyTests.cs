using System.Globalization;
using System.Security.Claims;
using Granit.Entities.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.Entities.Endpoints.Tests;

public sealed class EntityCacheKeyTests
{
    [Fact]
    public void ForManifest_segments_with_perms_hash_and_culture()
    {
        ClaimsPrincipal user = BuildUser(["Admin"], "user-1");

        string key = EntityCacheKey.ForManifest(
            "Granit.Parties.Party", user, CultureInfo.GetCultureInfo("fr"));

        key.ShouldStartWith("entity-meta:Granit.Parties.Party:");
        key.ShouldEndWith(":fr");
    }

    [Fact]
    public void ForManifest_returns_same_key_for_equal_perms()
    {
        ClaimsPrincipal a = BuildUser(["Admin", "Editor"], "user-1");
        ClaimsPrincipal b = BuildUser(["Editor", "Admin"], "user-1");

        string keyA = EntityCacheKey.ForManifest("X", a, CultureInfo.InvariantCulture);
        string keyB = EntityCacheKey.ForManifest("X", b, CultureInfo.InvariantCulture);

        keyA.ShouldBe(keyB);
    }

    [Fact]
    public void ForManifest_diverges_when_role_set_differs()
    {
        ClaimsPrincipal a = BuildUser(["Admin"], "user-1");
        ClaimsPrincipal b = BuildUser(["Editor"], "user-1");

        EntityCacheKey.ForManifest("X", a, CultureInfo.InvariantCulture)
            .ShouldNotBe(EntityCacheKey.ForManifest("X", b, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ForDiscovery_uses_discovery_prefix()
    {
        ClaimsPrincipal user = BuildUser(["User"], "user-1");

        string key = EntityCacheKey.ForDiscovery(user, CultureInfo.GetCultureInfo("en-GB"));

        key.ShouldStartWith("entity-discovery:");
        key.ShouldEndWith(":en-GB");
    }

    private static ClaimsPrincipal BuildUser(IEnumerable<string> roles, string sub)
    {
        List<Claim> claims = [new(ClaimTypes.NameIdentifier, sub)];
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        ClaimsIdentity identity = new(claims, "test");
        return new ClaimsPrincipal(identity);
    }
}
