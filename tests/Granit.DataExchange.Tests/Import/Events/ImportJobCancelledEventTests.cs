using Granit.Core.Events;
using Granit.DataExchange.Import.Events;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import.Events;

public sealed class ImportJobCancelledEventTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var jobId = Guid.NewGuid();

        ImportJobCancelledEvent evt = new(jobId, "Acme.PatientImport");

        evt.ImportJobId.ShouldBe(jobId);
        evt.DefinitionName.ShouldBe("Acme.PatientImport");
    }

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
