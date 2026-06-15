using Granit.Presence.Endpoints.Permissions;
using Shouldly;
using Xunit;

namespace Granit.Presence.Endpoints.Tests;

public sealed class PresencePermissionsTests
{
    [Fact]
    public void Self_Manage_constant_matches_canonical_form() =>
        PresencePermissions.Self.Manage.ShouldBe("Presence.Self.Manage");

    [Fact]
    public void Users_Read_constant_matches_canonical_form() =>
        PresencePermissions.Users.Read.ShouldBe("Presence.Users.Read");

    [Fact]
    public void Group_name_matches_section_name() =>
        PresencePermissions.GroupName.ShouldBe("Presence");
}
