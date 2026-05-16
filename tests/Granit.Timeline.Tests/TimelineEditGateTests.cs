// =============================================================================
// Tests — TimelineEditGate
// =============================================================================
// 4-gate matrix: external origin, SystemLog, authorship, edit window.
// Each gate maps to a specific TimelineEntryNotEditableReason so the front
// can render an actionable message.
// =============================================================================

using Granit.Domain;
using Granit.Timeline.Abstractions;
using Granit.Timeline.Domain;
using Granit.Timeline.Domain.ValueObjects;
using Granit.Timeline.Internal;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Tests;

public sealed class TimelineEditGateTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private static TimelineEntry NewComment(string authorId = "u-1", DateTimeOffset? createdAt = null) =>
        TimelineEntry.Create(
            Guid.NewGuid(),
            new EntityReference("User", "user-42"),
            TimelineEntryType.Comment,
            "hello",
            new AuthorInfo(authorId, "Alice"),
            createdAt ?? Now,
            authorId);

    [Fact]
    public void Passes_WhenAuthorEditsRecentNativeComment()
    {
        TimelineEntry entry = NewComment();
        Should.NotThrow(() => TimelineEditGate.EnsureEditable(entry, "u-1", Now, Window));
    }

    [Fact]
    public void Rejects_ExternalOrigin_OverEverythingElse()
    {
        var shadow = TimelineEntry.CreateShadow(
            Guid.NewGuid(),
            new EntityReference("User", "user-42"),
            "snapshot",
            new AuthorInfo("u-1", "Alice"),
            Now,
            "u-1",
            "auditing",
            "audit-1");

        TimelineEntryNotEditableException ex = Should.Throw<TimelineEntryNotEditableException>(() =>
            TimelineEditGate.EnsureEditable(shadow, "u-1", Now, Window));
        ex.Reason.ShouldBe(TimelineEntryNotEditableReason.ExternalOrigin);
    }

    [Fact]
    public void Rejects_SystemLogNative()
    {
        var systemLog = TimelineEntry.Create(
            Guid.NewGuid(),
            new EntityReference("User", "user-42"),
            TimelineEntryType.SystemLog,
            "system",
            new AuthorInfo("system", "System"),
            Now,
            "system");

        TimelineEntryNotEditableException ex = Should.Throw<TimelineEntryNotEditableException>(() =>
            TimelineEditGate.EnsureEditable(systemLog, "system", Now, Window));
        ex.Reason.ShouldBe(TimelineEntryNotEditableReason.SystemLog);
    }

    [Fact]
    public void Rejects_NotAuthor()
    {
        TimelineEntry entry = NewComment("u-1");
        TimelineEntryNotEditableException ex = Should.Throw<TimelineEntryNotEditableException>(() =>
            TimelineEditGate.EnsureEditable(entry, "u-2", Now, Window));
        ex.Reason.ShouldBe(TimelineEntryNotEditableReason.NotAuthor);
    }

    [Fact]
    public void Rejects_NullCurrentUser()
    {
        TimelineEntry entry = NewComment("u-1");
        TimelineEntryNotEditableException ex = Should.Throw<TimelineEntryNotEditableException>(() =>
            TimelineEditGate.EnsureEditable(entry, null, Now, Window));
        ex.Reason.ShouldBe(TimelineEntryNotEditableReason.NotAuthor);
    }

    [Fact]
    public void Rejects_WindowExpired()
    {
        TimelineEntry entry = NewComment("u-1", createdAt: Now - TimeSpan.FromHours(1));
        TimelineEntryNotEditableException ex = Should.Throw<TimelineEntryNotEditableException>(() =>
            TimelineEditGate.EnsureEditable(entry, "u-1", Now, Window));
        ex.Reason.ShouldBe(TimelineEntryNotEditableReason.WindowExpired);
    }

    [Fact]
    public void Rejects_WhenEditWindowIsZero()
    {
        TimelineEntry entry = NewComment("u-1");
        TimelineEntryNotEditableException ex = Should.Throw<TimelineEntryNotEditableException>(() =>
            TimelineEditGate.EnsureEditable(entry, "u-1", Now, TimeSpan.Zero));
        ex.Reason.ShouldBe(TimelineEntryNotEditableReason.WindowExpired);
    }
}
