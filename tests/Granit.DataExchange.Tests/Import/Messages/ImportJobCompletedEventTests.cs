using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Messages;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import.Messages;

public sealed class ImportJobCompletedEtoTests
{
    [Theory]
    [InlineData(ImportJobStatus.Completed)]
    [InlineData(ImportJobStatus.PartiallyCompleted)]
    [InlineData(ImportJobStatus.Failed)]
    [InlineData(ImportJobStatus.Cancelled)]
    public void Constructor_AcceptsTerminalStatuses(ImportJobStatus status)
    {
        var sut = new ImportJobCompletedEto(
            Guid.NewGuid(), "def", status, "user", 10, 5, 5, 3, 2, 0);

        sut.Status.ShouldBe(status);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var jobId = Guid.NewGuid();

        var a = new ImportJobCompletedEto(jobId, "d", ImportJobStatus.Completed, "u", 1, 1, 0, 1, 0, 0);
        var b = new ImportJobCompletedEto(jobId, "d", ImportJobStatus.Completed, "u", 1, 1, 0, 1, 0, 0);

        a.ShouldBe(b);
    }
}
