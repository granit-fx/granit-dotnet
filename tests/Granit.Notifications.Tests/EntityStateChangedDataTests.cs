// =============================================================================
// Tests - EntityStateChangedData
// =============================================================================
// Verifies the record type used by entity tracking notifications: required
// properties, optional properties, and record value equality.
// =============================================================================

using Shouldly;
using Xunit;

namespace Granit.Notifications.Tests;

public sealed class EntityStateChangedDataTests
{
    [Fact]
    public void OptionalProperties_DefaultToNull()
    {
        EntityStateChangedData data = new()
        {
            EntityType = "Patient",
            EntityId = "pat-42",
            PropertyName = "Status",
            ChangedAt = DateTimeOffset.UtcNow,
        };

        data.OldValue.ShouldBeNull();
        data.NewValue.ShouldBeNull();
        data.ChangedByUserId.ShouldBeNull();
    }

    [Fact]
    public void AllProperties_CanBeSetViaInitializers()
    {
        DateTimeOffset changedAt = new(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);

        EntityStateChangedData data = new()
        {
            EntityType = "Document",
            EntityId = "doc-99",
            PropertyName = "State",
            OldValue = "Draft",
            NewValue = "Published",
            ChangedAt = changedAt,
            ChangedByUserId = "user-admin",
        };

        data.OldValue.ShouldBe("Draft");
        data.NewValue.ShouldBe("Published");
        data.ChangedByUserId.ShouldBe("user-admin");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        DateTimeOffset changedAt = new(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);

        EntityStateChangedData a = new()
        {
            EntityType = "Patient",
            EntityId = "pat-1",
            PropertyName = "Name",
            OldValue = "Alice",
            NewValue = "Bob",
            ChangedAt = changedAt,
            ChangedByUserId = "user-1",
        };

        EntityStateChangedData b = new()
        {
            EntityType = "Patient",
            EntityId = "pat-1",
            PropertyName = "Name",
            OldValue = "Alice",
            NewValue = "Bob",
            ChangedAt = changedAt,
            ChangedByUserId = "user-1",
        };

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentNewValue_AreNotEqual()
    {
        DateTimeOffset changedAt = new(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);

        EntityStateChangedData a = new()
        {
            EntityType = "Patient",
            EntityId = "pat-1",
            PropertyName = "Name",
            OldValue = "Alice",
            NewValue = "Bob",
            ChangedAt = changedAt,
        };

        EntityStateChangedData b = new()
        {
            EntityType = "Patient",
            EntityId = "pat-1",
            PropertyName = "Name",
            OldValue = "Alice",
            NewValue = "Charlie",
            ChangedAt = changedAt,
        };

        a.ShouldNotBe(b);
    }
}
