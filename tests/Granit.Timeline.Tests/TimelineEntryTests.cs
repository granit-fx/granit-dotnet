using Granit.Domain;
using Granit.Timeline.Domain;
using Granit.Timeline.Events;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Tests;

public sealed class TimelineEntryTests
{
    [Fact]
    public void Create_Comment_SetsAllProperties()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        var entry = TimelineEntry.Create(
            id, "Patient", "p-1", TimelineEntryType.Comment,
            "Hello world", "user-1", "Alice", now, "user-1",
            tenantId, parentId);

        entry.Id.ShouldBe(id);
        entry.EntityType.ShouldBe("Patient");
        entry.EntityId.ShouldBe("p-1");
        entry.EntryType.ShouldBe(TimelineEntryType.Comment);
        entry.Body.ShouldBe("Hello world");
        entry.AuthorId.ShouldBe("user-1");
        entry.AuthorName.ShouldBe("Alice");
        entry.CreatedAt.ShouldBe(now);
        entry.CreatedBy.ShouldBe("user-1");
        entry.TenantId.ShouldBe(tenantId);
        entry.ParentEntryId.ShouldBe(parentId);
        entry.IsDeleted.ShouldBeFalse();
        entry.DeletedAt.ShouldBeNull();
        entry.DeletedBy.ShouldBeNull();
    }

    [Fact]
    public void Create_WithoutOptionalParams_DefaultsToNull()
    {
        var entry = TimelineEntry.Create(
            Guid.NewGuid(), "Invoice", "inv-1", TimelineEntryType.SystemLog,
            "{}", "system", "System", DateTimeOffset.UtcNow, "system");

        entry.TenantId.ShouldBeNull();
        entry.ParentEntryId.ShouldBeNull();
    }

    [Fact]
    public void RaisePostedEvent_AddsTimelineEntryPostedEvent()
    {
        var entry = TimelineEntry.Create(
            Guid.NewGuid(), "Patient", "p-1", TimelineEntryType.Comment,
            "Test", "user-1", "Alice", DateTimeOffset.UtcNow, "user-1");

        entry.RaisePostedEvent();

        TimelineEntryPostedEvent evt = entry.DomainEvents
            .ShouldHaveSingleItem()
            .ShouldBeOfType<TimelineEntryPostedEvent>();
        evt.EntryId.ShouldBe(entry.Id);
        evt.EntityType.ShouldBe("Patient");
        evt.EntityId.ShouldBe("p-1");
        evt.EntryType.ShouldBe(TimelineEntryType.Comment);
        evt.AuthorId.ShouldBe("user-1");
    }

    [Fact]
    public void SoftDelete_Comment_SetsDeletedFields()
    {
        var entry = TimelineEntry.Create(
            Guid.NewGuid(), "Patient", "p-1", TimelineEntryType.Comment,
            "Test", "user-1", "Alice", DateTimeOffset.UtcNow, "user-1");
        DateTimeOffset deletedAt = DateTimeOffset.UtcNow;

        entry.SoftDelete(deletedAt, "admin");

        entry.IsDeleted.ShouldBeTrue();
        entry.DeletedAt.ShouldBe(deletedAt);
        entry.DeletedBy.ShouldBe("admin");
    }

    [Fact]
    public void SoftDelete_Comment_RaisesTimelineEntrySoftDeletedEvent()
    {
        var entry = TimelineEntry.Create(
            Guid.NewGuid(), "Patient", "p-1", TimelineEntryType.Comment,
            "Test", "user-1", "Alice", DateTimeOffset.UtcNow, "user-1");

        entry.SoftDelete(DateTimeOffset.UtcNow, "admin");

        TimelineEntrySoftDeletedEvent evt = entry.DomainEvents
            .ShouldHaveSingleItem()
            .ShouldBeOfType<TimelineEntrySoftDeletedEvent>();
        evt.EntryId.ShouldBe(entry.Id);
        evt.EntityType.ShouldBe("Patient");
        evt.EntityId.ShouldBe("p-1");
    }

    [Fact]
    public void SoftDelete_InternalNote_SetsDeletedFields()
    {
        var entry = TimelineEntry.Create(
            Guid.NewGuid(), "Patient", "p-1", TimelineEntryType.InternalNote,
            "Staff note", "user-1", "Alice", DateTimeOffset.UtcNow, "user-1");

        entry.SoftDelete(DateTimeOffset.UtcNow, "admin");

        entry.IsDeleted.ShouldBeTrue();
    }

    [Fact]
    public void SoftDelete_SystemLog_ThrowsInvalidOperationException()
    {
        var entry = TimelineEntry.Create(
            Guid.NewGuid(), "Invoice", "inv-1", TimelineEntryType.SystemLog,
            "{}", "system", "System", DateTimeOffset.UtcNow, "system");

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(
            () => entry.SoftDelete(DateTimeOffset.UtcNow, "admin"));

        ex.Message.ShouldContain("immutable");
        ex.Message.ShouldContain("ISO 27001");
    }

    [Fact]
    public void SoftDelete_SystemLog_DoesNotEmitEvent()
    {
        var entry = TimelineEntry.Create(
            Guid.NewGuid(), "Invoice", "inv-1", TimelineEntryType.SystemLog,
            "{}", "system", "System", DateTimeOffset.UtcNow, "system");

        Should.Throw<InvalidOperationException>(
            () => entry.SoftDelete(DateTimeOffset.UtcNow, "admin"));

        entry.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void IMultiTenant_TenantId_CanBeSetExplicitly()
    {
        var entry = TimelineEntry.Create(
            Guid.NewGuid(), "Patient", "p-1", TimelineEntryType.Comment,
            "Test", "user-1", "Alice", DateTimeOffset.UtcNow, "user-1");

        var tenantId = Guid.NewGuid();
        ((IMultiTenant)entry).TenantId = tenantId;

        entry.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void ISoftDeletable_Properties_CanBeSetExplicitly()
    {
        var entry = TimelineEntry.Create(
            Guid.NewGuid(), "Patient", "p-1", TimelineEntryType.Comment,
            "Test", "user-1", "Alice", DateTimeOffset.UtcNow, "user-1");

        DateTimeOffset deletedAt = DateTimeOffset.UtcNow;
        ISoftDeletable softDeletable = entry;
        softDeletable.IsDeleted = true;
        softDeletable.DeletedAt = deletedAt;
        softDeletable.DeletedBy = "admin";

        entry.IsDeleted.ShouldBeTrue();
        entry.DeletedAt.ShouldBe(deletedAt);
        entry.DeletedBy.ShouldBe("admin");
    }
}
