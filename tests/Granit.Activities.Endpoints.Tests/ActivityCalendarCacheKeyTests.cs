using System.Security.Claims;
using Granit.Activities.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.Activities.Endpoints.Tests;

public sealed class ActivityCalendarCacheKeyTests
{
    private static readonly DateTimeOffset From = new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 5, 31, 23, 59, 59, TimeSpan.Zero);

    [Fact]
    public void Build_starts_with_prefix_and_includes_tenant()
    {
        ClaimsPrincipal user = BuildUser(["Admin"], "user-1");
        string key = ActivityCalendarCacheKey.Build(
            "abc-tenant", user, From, To,
            assigneeFilter: "me", entityTypeFilter: null, typeFilter: null, statusFilter: null);

        key.ShouldStartWith("activity-calendar:abc-tenant:");
    }

    [Fact]
    public void Build_uses_global_segment_when_tenant_null()
    {
        ClaimsPrincipal user = BuildUser([], "user-1");
        string key = ActivityCalendarCacheKey.Build(
            null, user, From, To, null, null, null, null);
        key.ShouldStartWith("activity-calendar:global:");
    }

    [Fact]
    public void Build_partitions_by_filter_arguments()
    {
        ClaimsPrincipal user = BuildUser(["Admin"], "user-1");
        string a = ActivityCalendarCacheKey.Build("t", user, From, To, "me", null, null, null);
        string b = ActivityCalendarCacheKey.Build("t", user, From, To, null, null, null, null);
        a.ShouldNotBe(b);
    }

    [Fact]
    public void Build_partitions_by_user_perms_hash()
    {
        ClaimsPrincipal admin = BuildUser(["Admin"], "user-1");
        ClaimsPrincipal viewer = BuildUser(["Viewer"], "user-1");
        ActivityCalendarCacheKey.Build("t", admin, From, To, null, null, null, null)
            .ShouldNotBe(ActivityCalendarCacheKey.Build("t", viewer, From, To, null, null, null, null));
    }

    [Fact]
    public void EvictionTag_per_tenant_format()
    {
        var tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
        ActivityCalendarCacheKey.EvictionTag(tenant)
            .ShouldBe($"activity-calendar:tenant:{tenant}");
    }

    [Fact]
    public void EvictionTag_uses_global_when_tenant_null()
    {
        ActivityCalendarCacheKey.EvictionTag(null)
            .ShouldBe("activity-calendar:tenant:global");
    }

    private static ClaimsPrincipal BuildUser(IEnumerable<string> roles, string sub)
    {
        List<Claim> claims = [new(ClaimTypes.NameIdentifier, sub)];
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }
}
