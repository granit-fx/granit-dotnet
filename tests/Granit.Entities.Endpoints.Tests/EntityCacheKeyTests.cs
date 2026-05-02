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

    [Fact]
    public void ForCalendarRange_segments_with_entity_calendar_perms_and_window()
    {
        ClaimsPrincipal user = BuildUser(["User"], "user-1");
        DateTimeOffset from = new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset to = new(2026, 5, 8, 0, 0, 0, TimeSpan.Zero);

        string key = EntityCacheKey.ForCalendarRange("Showcase.Meeting", null, user, from, to);

        key.ShouldStartWith("entity-calendar:Showcase.Meeting:_default:");
        key.ShouldEndWith($":{from.UtcTicks}-{to.UtcTicks}");
    }

    [Fact]
    public void ForCalendarRange_uses_explicit_calendar_name_when_provided()
    {
        ClaimsPrincipal user = BuildUser(["User"], "user-1");
        DateTimeOffset from = new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset to = new(2026, 5, 8, 0, 0, 0, TimeSpan.Zero);

        string key = EntityCacheKey.ForCalendarRange("Showcase.Meeting", "deadlines", user, from, to);

        key.ShouldStartWith("entity-calendar:Showcase.Meeting:deadlines:");
    }

    [Fact]
    public void ForCalendarRange_returns_same_key_for_identical_inputs()
    {
        ClaimsPrincipal user = BuildUser(["User"], "user-1");
        DateTimeOffset from = new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset to = new(2026, 5, 8, 0, 0, 0, TimeSpan.Zero);

        EntityCacheKey.ForCalendarRange("X", null, user, from, to)
            .ShouldBe(EntityCacheKey.ForCalendarRange("X", null, user, from, to));
    }

    [Fact]
    public void ForCalendarRange_diverges_when_window_or_perms_differ()
    {
        ClaimsPrincipal a = BuildUser(["Admin"], "user-1");
        ClaimsPrincipal b = BuildUser(["Editor"], "user-1");
        DateTimeOffset from = new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset to = new(2026, 5, 8, 0, 0, 0, TimeSpan.Zero);

        // Different window
        EntityCacheKey.ForCalendarRange("X", null, a, from, to)
            .ShouldNotBe(EntityCacheKey.ForCalendarRange("X", null, a, from, to.AddDays(1)));

        // Different role set
        EntityCacheKey.ForCalendarRange("X", null, a, from, to)
            .ShouldNotBe(EntityCacheKey.ForCalendarRange("X", null, b, from, to));
    }

    private static ClaimsPrincipal BuildUser(IEnumerable<string> roles, string sub)
    {
        List<Claim> claims = [new(ClaimTypes.NameIdentifier, sub)];
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        ClaimsIdentity identity = new(claims, "test");
        return new ClaimsPrincipal(identity);
    }
}
