using Granit.BackgroundJobs.Abstractions;
using Granit.Domain.ValueObjects;
using Granit.Privacy.BackgroundJobs.Jobs;
using Granit.Privacy.DataExport.Events;
using NSubstitute;
using Xunit;

namespace Granit.Privacy.BackgroundJobs.Tests.Jobs;

public sealed class PrivacyExportCompletedHandlerTests
{
    [Fact]
    public async Task HandleAsync_PublishesAssemblyJob_CarryingTheCompletionEvent()
    {
        IBackgroundJobDispatcher dispatcher = Substitute.For<IBackgroundJobDispatcher>();
        ExportCompletedEto completion = BuildEvent();

        await PrivacyExportCompletedHandler.HandleAsync(completion, dispatcher, TestContext.Current.CancellationToken);

        await dispatcher.Received(1).PublishAsync(
            Arg.Is<PrivacyExportAssemblyJob>(job => job.Event == completion),
            headers: null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_PropagatesCancellationToken()
    {
        IBackgroundJobDispatcher dispatcher = Substitute.For<IBackgroundJobDispatcher>();
        ExportCompletedEto completion = BuildEvent();
        using CancellationTokenSource cts = new();

        await PrivacyExportCompletedHandler.HandleAsync(completion, dispatcher, cts.Token);

        await dispatcher.Received(1).PublishAsync(
            Arg.Any<PrivacyExportAssemblyJob>(),
            headers: null,
            cts.Token);
    }

    private static ExportCompletedEto BuildEvent() => new(
        RequestId: Guid.NewGuid(),
        UserId: Guid.NewGuid(),
        ArchiveBlobReferenceId: BlobReference.Create($"personal-data-export/{Guid.NewGuid()}"),
        IsPartial: false,
        MissingProviders: [],
        Fragments: [],
        Regulation: "EU_GDPR",
        RequestedAt: DateTimeOffset.UtcNow,
        TenantId: Guid.NewGuid());
}
