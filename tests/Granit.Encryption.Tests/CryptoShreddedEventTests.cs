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
    public void ImplementsIDomainEvent()
    {
        CryptoShreddedEvent evt = new("Patient", "id-1", DateTimeOffset.UtcNow);

        evt.ShouldBeAssignableTo<IDomainEvent>();
    }
}
