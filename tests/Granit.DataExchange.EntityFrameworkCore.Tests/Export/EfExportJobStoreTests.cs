using Granit.DataExchange.EntityFrameworkCore.Internal.Export.Stores;
using Granit.DataExchange.EntityFrameworkCore.Tests.Infrastructure;
using Granit.DataExchange.Export;
using Granit.DataExchange.Export.Domain;
using Granit.MultiTenancy;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.EntityFrameworkCore.Tests.Export;

public sealed class EfExportJobStoreTests
{
    private static string NewDb() => Guid.NewGuid().ToString();

    // ── CreateAsync + GetAsync ──────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_then_GetAsync_returns_job()
    {
        // Arrange
        string dbName = NewDb();
        EfExportJobStore sut = CreateStore(dbName);
        ExportJob job = BuildJob();

        // Act
        await sut.CreateAsync(job, TestContext.Current.CancellationToken);
        ExportJob? loaded = await sut.GetAsync(job.Id, TestContext.Current.CancellationToken);

        // Assert
        loaded.ShouldNotBeNull();
        loaded!.Id.ShouldBe(job.Id);
        loaded.DefinitionName.ShouldBe("Test.Export");
        loaded.Format.ShouldBe("csv");
        loaded.Status.ShouldBe(ExportJobStatus.Queued);
        loaded.RequestJson.ShouldBe(job.RequestJson);
    }

    [Fact]
    public async Task GetAsync_unknown_id_returns_null()
    {
        // Arrange
        string dbName = NewDb();
        EfExportJobStore sut = CreateStore(dbName);

        // Act
        ExportJob? result = await sut.GetAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    // ── UpdateAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_persists_status_change()
    {
        // Arrange
        string dbName = NewDb();
        EfExportJobStore sut = CreateStore(dbName);
        ExportJob job = BuildJob();
        await sut.CreateAsync(job, TestContext.Current.CancellationToken);

        // Act
        job.MarkAsExporting();
        job.Complete("blob-123", "export.csv", 42, DateTimeOffset.UtcNow);
        await sut.UpdateAsync(job, TestContext.Current.CancellationToken);

        // Assert
        ExportJob? loaded = await sut.GetAsync(job.Id, TestContext.Current.CancellationToken);
        loaded.ShouldNotBeNull();
        loaded!.Status.ShouldBe(ExportJobStatus.Completed);
        loaded.RowCount.ShouldBe(42);
        loaded.FileName.ShouldBe("export.csv");
        loaded.BlobReference.ShouldBe("blob-123");
        loaded.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_persists_failure()
    {
        // Arrange
        string dbName = NewDb();
        EfExportJobStore sut = CreateStore(dbName);
        ExportJob job = BuildJob();
        await sut.CreateAsync(job, TestContext.Current.CancellationToken);

        // Act
        job.MarkAsExporting();
        job.Fail("Something went wrong", DateTimeOffset.UtcNow);
        await sut.UpdateAsync(job, TestContext.Current.CancellationToken);

        // Assert
        ExportJob? loaded = await sut.GetAsync(job.Id, TestContext.Current.CancellationToken);
        loaded.ShouldNotBeNull();
        loaded!.Status.ShouldBe(ExportJobStatus.Failed);
        loaded.ErrorMessage.ShouldBe("Something went wrong");
    }

    [Fact]
    public async Task UpdateAsync_status_transitions()
    {
        // Arrange
        string dbName = NewDb();
        EfExportJobStore sut = CreateStore(dbName);
        ExportJob job = BuildJob();
        await sut.CreateAsync(job, TestContext.Current.CancellationToken);

        // Act — Queued → Exporting → Completed
        job.MarkAsExporting();
        await sut.UpdateAsync(job, TestContext.Current.CancellationToken);

        ExportJob? interim = await sut.GetAsync(job.Id, TestContext.Current.CancellationToken);
        interim.ShouldNotBeNull();
        interim!.Status.ShouldBe(ExportJobStatus.Exporting);

        job.Complete("blob-ref", "export.csv", 10, DateTimeOffset.UtcNow);
        await sut.UpdateAsync(job, TestContext.Current.CancellationToken);

        ExportJob? final = await sut.GetAsync(job.Id, TestContext.Current.CancellationToken);
        final.ShouldNotBeNull();
        final!.Status.ShouldBe(ExportJobStatus.Completed);
        final.RowCount.ShouldBe(10);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static EfExportJobStore CreateStore(string dbName)
    {
        InMemoryDataExchangeContextFactory factory = new(dbName);
        return new EfExportJobStore(factory, Substitute.For<ICurrentTenant>());
    }

    private static ExportJob BuildJob() =>
        ExportJob.Create(
            Guid.NewGuid(),
            "Test.Export",
            "csv",
            """{"DefinitionName":"Test.Export","Format":"csv"}""");
}
