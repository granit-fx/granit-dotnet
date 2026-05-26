using Granit.Domain.ValueObjects;
using Granit.MultiTenancy;
using Granit.Privacy.BackgroundJobs.Jobs;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Events;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Privacy.BackgroundJobs.Tests.Jobs;

public sealed class PrivacyExportAssemblyJobHandlerTests
{
    [Fact]
    public async Task HandleAsync_OpensTenantScope_BeforeInvokingService()
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        IDisposable scope = Substitute.For<IDisposable>();
        var tenantId = Guid.NewGuid();
        currentTenant.Change(tenantId).Returns(scope);

        Guid? observedTenantInsideService = null;
        IPrivacyExportAssemblyService service = Substitute.For<IPrivacyExportAssemblyService>();
        service
            .AssembleAsync(Arg.Any<ExportCompletedEto>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                observedTenantInsideService = currentTenant.Id;
                return Task.CompletedTask;
            });

        PrivacyExportAssemblyJob job = new(BuildEvent(tenantId));

        await PrivacyExportAssemblyJobHandler.HandleAsync(
            job, currentTenant, service, TestContext.Current.CancellationToken);

        currentTenant.Received(1).Change(tenantId);
        await service.Received(1).AssembleAsync(job.Event, Arg.Any<CancellationToken>());
        scope.Received(1).Dispose();
    }

    [Fact]
    public async Task HandleAsync_PassesEventVerbatim_ToService()
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Change(Arg.Any<Guid?>()).Returns(Substitute.For<IDisposable>());
        IPrivacyExportAssemblyService service = Substitute.For<IPrivacyExportAssemblyService>();
        ExportCompletedEto @event = BuildEvent(tenantId: null);

        await PrivacyExportAssemblyJobHandler.HandleAsync(
            new PrivacyExportAssemblyJob(@event),
            currentTenant,
            service,
            TestContext.Current.CancellationToken);

        await service.Received(1).AssembleAsync(@event, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Job_Exposes_RequestId_And_TenantId_From_Event()
    {
        var tenantId = Guid.NewGuid();
        ExportCompletedEto @event = BuildEvent(tenantId);
        PrivacyExportAssemblyJob job = new(@event);

        job.RequestId.ShouldBe(@event.RequestId);
        job.TenantId.ShouldBe(tenantId);
    }

    private static ExportCompletedEto BuildEvent(Guid? tenantId) => new(
        RequestId: Guid.NewGuid(),
        UserId: Guid.NewGuid(),
        ArchiveBlobReferenceId: BlobReference.Create($"personal-data-export/{Guid.NewGuid()}"),
        IsPartial: false,
        MissingProviders: [],
        Fragments: [],
        Regulation: "EU_GDPR",
        RequestedAt: DateTimeOffset.UtcNow,
        TenantId: tenantId);
}
