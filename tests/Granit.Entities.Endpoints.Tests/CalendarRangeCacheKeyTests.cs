using System.Globalization;
using System.Security.Claims;
using Granit.Entities.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.Entities.Endpoints.Tests;

public sealed class CalendarRangeCacheKeyTests
{
    private static readonly DateTimeOffset From = new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 5, 7, 23, 59, 59, TimeSpan.Zero);

    [Fact]
    public void Build_includes_every_partitioning_segment()
    {
        ClaimsPrincipal user = BuildUser(["Admin"], "user-1");
        string key = CalendarRangeCacheKey.Build(
            "Granit.Parties.Party", "primary", From, To, user,
            CultureInfo.GetCultureInfo("en-GB"));

        key.ShouldStartWith("calendar-range:Granit.Parties.Party:primary:");
        key.ShouldEndWith(":en-GB");
    }

    [Fact]
    public void Build_uses_wildcard_when_calendar_unspecified()
    {
        string key = CalendarRangeCacheKey.Build(
            "Granit.Parties.Party", calendarName: null, From, To,
            BuildUser(["Admin"], "u"), CultureInfo.InvariantCulture);

        key.ShouldContain(":*:");
    }

    [Fact]
    public void Build_partitions_by_user_perms_hash()
    {
        ClaimsPrincipal admin = BuildUser(["Admin"], "user-1");
        ClaimsPrincipal viewer = BuildUser(["Viewer"], "user-1");

        string adminKey = CalendarRangeCacheKey.Build(
            "X", "c", From, To, admin, CultureInfo.InvariantCulture);
        string viewerKey = CalendarRangeCacheKey.Build(
            "X", "c", From, To, viewer, CultureInfo.InvariantCulture);

        adminKey.ShouldNotBe(viewerKey);
    }

    [Fact]
    public void Build_partitions_by_window()
    {
        ClaimsPrincipal user = BuildUser(["Admin"], "user-1");

        string week1 = CalendarRangeCacheKey.Build("X", "c", From, To, user, CultureInfo.InvariantCulture);
        string week2 = CalendarRangeCacheKey.Build("X", "c", From.AddDays(7), To.AddDays(7), user, CultureInfo.InvariantCulture);

        week1.ShouldNotBe(week2);
    }

    [Fact]
    public void EvictionTag_is_per_entity()
    {
        CalendarRangeCacheKey.EvictionTag("Granit.Parties.Party")
            .ShouldBe("calendar:Granit.Parties.Party");
    }

    private static ClaimsPrincipal BuildUser(IEnumerable<string> roles, string sub)
    {
        List<Claim> claims = [new(ClaimTypes.NameIdentifier, sub)];
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }
}
