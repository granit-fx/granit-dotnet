// =============================================================================
// CryptoShreddedEventTests - Domain event for crypto-shredding
// =============================================================================
// Verifies:
//   - Record properties are set correctly
//   - Implements IDomainEvent
//   - Value equality semantics (record behavior)
// =============================================================================

using Granit.Encryption.Events;
using Granit.Events;
using Shouldly;
using Xunit;

namespace Granit.Encryption.Tests;

public sealed class CryptoShreddedEventTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        CryptoShreddedEvent evt = new("Patient", "id-42", now);

        evt.EntityType.ShouldBe("Patient");
        evt.EntityId.ShouldBe("id-42");
        evt.ShreddedAt.ShouldBe(now);
    }

    [Fact]
    public void ImplementsIDomainEvent()
    {
        CryptoShreddedEvent evt = new("Patient", "id-1", DateTimeOffset.UtcNow);

        evt.ShouldBeAssignableTo<IDomainEvent>();
    }

    [Fact]
    public void EqualityByValue_SameProperties_AreEqual()
    {
        DateTimeOffset timestamp = new(2026, 3, 27, 12, 0, 0, TimeSpan.Zero);

        CryptoShreddedEvent a = new("Patient", "id-1", timestamp);
        CryptoShreddedEvent b = new("Patient", "id-1", timestamp);

        a.ShouldBe(b);
    }

    [Fact]
    public void EqualityByValue_DifferentEntityId_AreNotEqual()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        CryptoShreddedEvent a = new("Patient", "id-1", timestamp);
        CryptoShreddedEvent b = new("Patient", "id-2", timestamp);

        a.ShouldNotBe(b);
    }

    [Fact]
    public void EqualityByValue_DifferentEntityType_AreNotEqual()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        CryptoShreddedEvent a = new("Patient", "id-1", timestamp);
        CryptoShreddedEvent b = new("Order", "id-1", timestamp);

        a.ShouldNotBe(b);
    }
}
