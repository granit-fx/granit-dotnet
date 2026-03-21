using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Messages;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import.Messages;

public sealed class ImportJobCompletedEtoTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var jobId = Guid.NewGuid();

        var sut = new ImportJobCompletedEto(
            jobId,
            "CustomerImport",
            ImportJobStatus.Completed,
            "user-123",
            TotalRows: 100,
            SucceededRows: 95,
            FailedRows: 5,
            InsertedRows: 80,
            UpdatedRows: 15,
            SkippedRows: 0);

        sut.ImportJobId.ShouldBe(jobId);
        sut.DefinitionName.ShouldBe("CustomerImport");
        sut.Status.ShouldBe(ImportJobStatus.Completed);
        sut.UserId.ShouldBe("user-123");
        sut.TotalRows.ShouldBe(100);
        sut.SucceededRows.ShouldBe(95);
        sut.FailedRows.ShouldBe(5);
        sut.InsertedRows.ShouldBe(80);
        sut.UpdatedRows.ShouldBe(15);
        sut.SkippedRows.ShouldBe(0);
    }

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
