using Granit.DataExchange.Export.Events;
using Granit.Events;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Export.Events;

public sealed class ExportJobFailedEtoTests
{
    [Fact]
    public void Implements_IIntegrationEvent()
    {
        ExportJobFailedEto eto = new(Guid.NewGuid(), "def", "error");
        eto.ShouldBeAssignableTo<IIntegrationEvent>();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var jobId = Guid.NewGuid();

        ExportJobFailedEto a = new(jobId, "def", "error");
        ExportJobFailedEto b = new(jobId, "def", "error");

        a.ShouldBe(b);
    }
}
