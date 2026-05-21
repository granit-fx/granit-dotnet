using Granit.DataExchange.EntityFrameworkCore.Internal;
using Granit.DataExchange.EntityFrameworkCore.Internal.Import.Stores;
using Granit.DataExchange.EntityFrameworkCore.Tests.Infrastructure;
using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Mapping;
using Granit.DataExchange.Import.Reporting;
using Granit.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.EntityFrameworkCore.Tests.Stores;

public sealed class EfImportJobStoreTests
{
    private static string NewDb() => Guid.NewGuid().ToString();

    private static EfImportJobStore CreateStore(string dbName) =>
        new(new InMemoryDataExchangeContextFactory(dbName), Substitute.For<ICurrentTenant>());

    private static ImportJob CreateJob(Guid? id = null) =>
        CreateJobWithTenant(tenantId: null, id);

    private static ImportJob CreateJobWithTenant(Guid? tenantId, Guid? id = null)
    {
        var job = ImportJob.Create(
            id ?? Guid.NewGuid(),
            "Test.Import",
            "TestEntity",
            "test.csv",
            "text/csv",
            1024,
            "imports/test.csv",
            tenantId);
        job.CreatedAt = DateTimeOffset.UtcNow;
        job.CreatedBy = "test-user";
        return job;
    }

    [Fact]
    public async Task GetAsync_returns_null_when_not_found()
    {
        // Arrange
        EfImportJobStore store = CreateStore(NewDb());

        // Act
        ImportJob? result = await store.GetAsync(
            Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task CreateAsync_persists_job()
    {
        // Arrange
        string dbName = NewDb();
        EfImportJobStore store = CreateStore(dbName);
        ImportJob job = CreateJob();

        // Act
        await store.CreateAsync(job, TestContext.Current.CancellationToken);

        // Assert
        await using DataExchangeDbContext context = new InMemoryDataExchangeContextFactory(dbName).CreateDbContext();
        ImportJob? persisted = await context.ImportJobs
            .FirstOrDefaultAsync(j => j.Id == job.Id, TestContext.Current.CancellationToken);
        persisted.ShouldNotBeNull();
        persisted.DefinitionName.ShouldBe("Test.Import");
    }

    [Fact]
    public async Task GetAsync_returns_persisted_job()
    {
        // Arrange
        string dbName = NewDb();
        EfImportJobStore store = CreateStore(dbName);
        ImportJob job = CreateJob();
        await store.CreateAsync(job, TestContext.Current.CancellationToken);

        // Act
        ImportJob? result = await store.GetAsync(
            job.Id, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(job.Id);
        result.EntityTypeName.ShouldBe("TestEntity");
        result.Status.ShouldBe(ImportJobStatus.Created);
    }

    [Fact]
    public async Task UpdateAsync_modifies_existing_job()
    {
        // Arrange
        string dbName = NewDb();
        EfImportJobStore store = CreateStore(dbName);
        ImportJob job = CreateJob();
        await store.CreateAsync(job, TestContext.Current.CancellationToken);

        // Act
        job.MarkAsPreviewed();
        job.ConfirmMappings([]);
        job.MarkAsExecuting();
        job.ModifiedAt = DateTimeOffset.UtcNow;
        job.ModifiedBy = "system";
        await store.UpdateAsync(job, TestContext.Current.CancellationToken);

        // Assert
        ImportJob? result = await store.GetAsync(
            job.Id, TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Status.ShouldBe(ImportJobStatus.Executing);
    }

    [Fact]
    public async Task CreateAsync_and_GetAsync_roundtrip_all_fields()
    {
        // Arrange
        string dbName = NewDb();
        EfImportJobStore store = CreateStore(dbName);
        var tenantId = Guid.NewGuid();
        ImportJob job = CreateJobWithTenant(tenantId);
        ImportColumnMapping[] mappings = [new("A", null, MappingConfidence.Manual)];
        ImportReport report = new()
        {
            TotalRows = 100,
            SucceededRows = 100,
            FailedRows = 0,
            SkippedRows = 0,
            InsertedRows = 100,
            UpdatedRows = 0,
            Duration = TimeSpan.Zero,
            FinalStatus = ImportJobStatus.Completed,
            RowErrors = [],
        };
        // Use internal behavior methods to set state through proper lifecycle
        job.MarkAsPreviewed();
        job.ConfirmMappings(mappings);
        job.MarkAsExecuting();
        job.Complete(ImportJobStatus.Completed, report, DateTimeOffset.UtcNow);

        // Act
        await store.CreateAsync(job, TestContext.Current.CancellationToken);
        ImportJob? result = await store.GetAsync(
            job.Id, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.TenantId.ShouldBe(tenantId);
        result.Mappings.ShouldBe(mappings);
        result.Report.ShouldNotBeNull();
        result.Report.TotalRows.ShouldBe(100);
        result.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_lifecycle_status_transitions()
    {
        // Arrange
        string dbName = NewDb();
        EfImportJobStore store = CreateStore(dbName);
        ImportJob job = CreateJob();
        await store.CreateAsync(job, TestContext.Current.CancellationToken);

        // Act — simulate full lifecycle using behavior methods
        job.MarkAsPreviewed();
        job.ConfirmMappings([]);
        job.MarkAsExecuting();
        await store.UpdateAsync(job, TestContext.Current.CancellationToken);

        job.Complete(ImportJobStatus.Completed, new ImportReport
        {
            TotalRows = 0,
            SucceededRows = 0,
            FailedRows = 0,
            SkippedRows = 0,
            InsertedRows = 0,
            UpdatedRows = 0,
            Duration = TimeSpan.Zero,
            FinalStatus = ImportJobStatus.Completed,
            RowErrors = [],
        }, DateTimeOffset.UtcNow);
        await store.UpdateAsync(job, TestContext.Current.CancellationToken);

        // Assert
        ImportJob? result = await store.GetAsync(
            job.Id, TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Status.ShouldBe(ImportJobStatus.Completed);
        result.CompletedAt.ShouldNotBeNull();
    }
}
