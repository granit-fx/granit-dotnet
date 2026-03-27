using Granit.Timeline.Endpoints.Permissions;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Endpoints.Tests;

/// <summary>
/// Unit tests verifying <see cref="TimelinePermissions"/> constants.
/// </summary>
public sealed class TimelinePermissionsTests
{
    [Fact]
    public void GroupName_is_Timeline() =>
        TimelinePermissions.GroupName.ShouldBe("Timeline");

    [Fact]
    public void Entries_Read_is_Timeline_Entries_Read() =>
        TimelinePermissions.Entries.Read.ShouldBe("Timeline.Entries.Read");

    [Fact]
    public void Entries_Create_is_Timeline_Entries_Create() =>
        TimelinePermissions.Entries.Create.ShouldBe("Timeline.Entries.Create");

    [Fact]
    public void Entries_Manage_is_Timeline_Entries_Manage() =>
        TimelinePermissions.Entries.Manage.ShouldBe("Timeline.Entries.Manage");

    [Fact]
    public void InternalNotes_Read_is_Timeline_InternalNotes_Read() =>
        TimelinePermissions.InternalNotes.Read.ShouldBe("Timeline.InternalNotes.Read");

    [Fact]
    public void Followers_Manage_is_Timeline_Followers_Manage() =>
        TimelinePermissions.Followers.Manage.ShouldBe("Timeline.Followers.Manage");
}
