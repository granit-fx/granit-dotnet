// =============================================================================
// Tests - NotificationDeliveryException
// =============================================================================
// Verifies both constructors: message-only and message-with-inner-exception.
// Ensures proper message propagation and inner exception chaining.
// =============================================================================

using Granit.Notifications.Exceptions;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Tests;

public sealed class NotificationDeliveryExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_SetsMessageProperty()
    {
        NotificationDeliveryException exception = new("Delivery failed for channel Email");

        exception.Message.ShouldBe("Delivery failed for channel Email");
    }

    [Fact]
    public void Constructor_WithMessage_InnerExceptionIsNull()
    {
        NotificationDeliveryException exception = new("Delivery failed");

        exception.InnerException.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithMessageAndInnerException_SetsMessage()
    {
        InvalidOperationException inner = new("SMTP timeout");

        NotificationDeliveryException exception = new("Delivery failed", inner);

        exception.Message.ShouldBe("Delivery failed");
    }

    [Fact]
    public void Constructor_WithMessageAndInnerException_SetsInnerException()
    {
        InvalidOperationException inner = new("SMTP timeout");

        NotificationDeliveryException exception = new("Delivery failed", inner);

        exception.InnerException.ShouldBe(inner);
        exception.InnerException.ShouldBeOfType<InvalidOperationException>();
    }
}
