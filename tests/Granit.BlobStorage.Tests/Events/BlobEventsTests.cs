using Granit.BlobStorage.Events;
using Granit.Events;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Tests.Events;

public sealed class BlobEventsTests
{
    // ── BlobUploadStartedEvent ────────────────────────────────────────────────

    [Fact]
    public void BlobUploadStartedEvent_ImplementsIDomainEvent()
    {
        BlobUploadStartedEvent evt = new(Guid.NewGuid(), "c", "f");

        evt.ShouldBeAssignableTo<IDomainEvent>();
    }

    // ── BlobValidatedEvent ───────────────────────────────────────────────────

    [Fact]
    public void BlobValidatedEvent_ImplementsIDomainEvent()
    {
        BlobValidatedEvent evt = new(Guid.NewGuid(), "c", "t", 0);

        evt.ShouldBeAssignableTo<IDomainEvent>();
    }

    // ── BlobRejectedEvent ────────────────────────────────────────────────────

    [Fact]
    public void BlobRejectedEvent_ImplementsIDomainEvent()
    {
        BlobRejectedEvent evt = new(Guid.NewGuid(), "c", "r");

        evt.ShouldBeAssignableTo<IDomainEvent>();
    }

    // ── BlobDeletedEvent ─────────────────────────────────────────────────────

    [Fact]
    public void BlobDeletedEvent_ImplementsIDomainEvent()
    {
        BlobDeletedEvent evt = new(Guid.NewGuid(), "c", null);

        evt.ShouldBeAssignableTo<IDomainEvent>();
    }
}
