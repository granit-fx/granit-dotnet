using System.Globalization;
using System.Security.Claims;
using Granit.Entities.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.Entities.Endpoints.Tests;

public sealed class RelationAggregateCacheKeyTests
{
    [Fact]
    public void Build_includes_every_partitioning_segment()
    {
        ClaimsPrincipal user = BuildUser(["Admin"], "user-1");
        string key = RelationAggregateCacheKey.Build(
            "Granit.Parties.Party", "abc-123", "invoices", user,
            CultureInfo.GetCultureInfo("en-GB"));

        key.ShouldStartWith("relation-agg:Granit.Parties.Party:abc-123:invoices:");
        key.ShouldEndWith(":en-GB");
    }

    [Fact]
    public void Build_partitions_by_user_perms_hash()
    {
        ClaimsPrincipal admin = BuildUser(["Admin"], "user-1");
        ClaimsPrincipal viewer = BuildUser(["Viewer"], "user-1");

        string adminKey = RelationAggregateCacheKey.Build(
            "X", "1", "invoices", admin, CultureInfo.InvariantCulture);
        string viewerKey = RelationAggregateCacheKey.Build(
            "X", "1", "invoices", viewer, CultureInfo.InvariantCulture);

        adminKey.ShouldNotBe(viewerKey);
    }

    [Fact]
    public void Build_returns_same_key_for_role_reordering()
    {
        ClaimsPrincipal a = BuildUser(["Admin", "Editor"], "user-1");
        ClaimsPrincipal b = BuildUser(["Editor", "Admin"], "user-1");

        RelationAggregateCacheKey.Build("X", "1", "r", a, CultureInfo.InvariantCulture)
            .ShouldBe(RelationAggregateCacheKey.Build("X", "1", "r", b, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void EvictionTag_is_per_source_row()
    {
        RelationAggregateCacheKey.EvictionTag("Granit.Parties.Party", "abc-123")
            .ShouldBe("entity:Granit.Parties.Party:abc-123");
    }

    private static ClaimsPrincipal BuildUser(IEnumerable<string> roles, string sub)
    {
        List<Claim> claims = [new(ClaimTypes.NameIdentifier, sub)];
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }
}
