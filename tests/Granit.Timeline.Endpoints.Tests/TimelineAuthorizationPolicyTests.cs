using Granit.Timeline.Endpoints.Internal;
using Granit.Timeline.Endpoints.Permissions;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Endpoints.Tests;

public sealed class TimelineAuthorizationPolicyTests
{
    [Fact]
    public void PolicyName_Equals_Entries_Read() => TimelineAuthorizationPolicy.PolicyName.ShouldBe(TimelinePermissions.Entries.Read);

    [Fact]
    public void PolicyName_Value_is_Timeline_Entries_Read() => TimelineAuthorizationPolicy.PolicyName.ShouldBe("Timeline.Entries.Read");
}
