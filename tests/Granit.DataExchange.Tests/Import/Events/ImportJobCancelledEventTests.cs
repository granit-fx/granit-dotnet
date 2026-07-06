using Granit.DataExchange.Import.Events;
using Granit.Events;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import.Events;

public sealed class ImportJobCancelledEventTests
{
    [Fact]
    public void Implements_IDomainEvent()
    {
        ImportJobCancelledEvent evt = new(Guid.NewGuid(), "def");
        evt.ShouldBeAssignableTo<IDomainEvent>();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var jobId = Guid.NewGuid();

        ImportJobCancelledEvent a = new(jobId, "def");
        ImportJobCancelledEvent b = new(jobId, "def");

        a.ShouldBe(b);
    }
}
