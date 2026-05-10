using Granit.BackgroundJobs;
using Granit.Documents;
using Granit.Documents.BackgroundJobs.Jobs;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Documents.BackgroundJobs.Tests.Jobs;

public sealed class DocumentsBackgroundJobsTests
{
    [Theory]
    [InlineData(typeof(OrphanDocumentCleanupJob), "0 * * * *", "documents-orphan-cleanup")]
    [InlineData(typeof(EmptyTrashJob), "0 3 * * *", "documents-empty-trash")]
    [InlineData(typeof(QuotaRecomputeJob), "0 4 * * 0", "documents-quota-recompute")]
    public void Job_should_carry_RecurringJob_attribute(Type jobType, string expectedCron, string expectedName)
    {
        var attr = (RecurringJobAttribute?)Attribute.GetCustomAttribute(jobType, typeof(RecurringJobAttribute));

        attr.ShouldNotBeNull();
        attr!.CronExpression.ShouldBe(expectedCron);
        attr.Name.ShouldBe(expectedName);
        typeof(IBackgroundJob).IsAssignableFrom(jobType).ShouldBeTrue();
    }

    [Fact]
    public async Task OrphanDocumentCleanupHandler_should_delegate_to_maintenance_service()
    {
        IDocumentMaintenanceService maintenance = Substitute.For<IDocumentMaintenanceService>();
        maintenance.CleanupOrphanBlobsAsync(Arg.Any<CancellationToken>()).Returns(2);

        await OrphanDocumentCleanupHandler.HandleAsync(
            new OrphanDocumentCleanupJob(),
            maintenance,
            TestContext.Current.CancellationToken);

        await maintenance.Received(1).CleanupOrphanBlobsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmptyTrashHandler_should_delegate_to_maintenance_service()
    {
        IDocumentMaintenanceService maintenance = Substitute.For<IDocumentMaintenanceService>();

        await EmptyTrashHandler.HandleAsync(
            new EmptyTrashJob(),
            maintenance,
            TestContext.Current.CancellationToken);

        await maintenance.Received(1).EmptyTrashAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QuotaRecomputeHandler_should_delegate_to_maintenance_service()
    {
        IDocumentMaintenanceService maintenance = Substitute.For<IDocumentMaintenanceService>();

        await QuotaRecomputeHandler.HandleAsync(
            new QuotaRecomputeJob(),
            maintenance,
            TestContext.Current.CancellationToken);

        await maintenance.Received(1).RecomputeQuotasAsync(Arg.Any<CancellationToken>());
    }
}
