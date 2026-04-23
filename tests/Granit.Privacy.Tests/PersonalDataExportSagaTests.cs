using System.Diagnostics.Metrics;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Events;
using Granit.Privacy.DataExport.Internal;
using Granit.Privacy.Diagnostics;
using Granit.Privacy.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Wolverine;
using Xunit;
using Xunit.v3;

namespace Granit.Privacy.Tests;

public sealed class PersonalDataExportSagaTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly PrivacyMetrics _metrics;

    public PersonalDataExportSagaTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _metrics = new PrivacyMetrics(_sp.GetRequiredService<IMeterFactory>());
    }

    public void Dispose() => _sp.Dispose();

    private static IOptions<GranitPrivacyOptions> DefaultOptions() =>
        Microsoft.Extensions.Options.Options.Create(new GranitPrivacyOptions { ExportTimeoutMinutes = 5 });

    private static DataProviderRegistry BuildRegistry(params string[] providers)
    {
        DataProviderRegistry registry = new();
        foreach (string p in providers)
        {
            registry.Register(p);
        }

        return registry;
    }

    // -------------------------------------------------------------------------
    // Scénario 1 : export complet avec plusieurs modules
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StartAsync_InitializesState_FromEvent()
    {
        PersonalDataExportSaga saga = new();
        IMessageContext context = Substitute.For<IMessageContext>();
        DataProviderRegistry registry = BuildRegistry("patients", "billing");
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        PersonalDataRequestedEto evt = new(requestId, userId, DateTimeOffset.UtcNow, "EU_GDPR");

        await saga.Start(evt, registry, DefaultOptions(), context, _metrics);

        saga.Id.ShouldBe(requestId);
        saga.UserId.ShouldBe(userId);
        saga.ExpectedCount.ShouldBe(2);
        saga.PendingProviders.ShouldContain("patients");
        saga.PendingProviders.ShouldContain("billing");
    }

    [Fact]
    public async Task StartAsync_SchedulesTimeout_ViaPublishAsync()
    {
        PersonalDataExportSaga saga = new();
        IMessageContext context = Substitute.For<IMessageContext>();
        DataProviderRegistry registry = BuildRegistry("patients", "billing");
        var requestId = Guid.NewGuid();
        PersonalDataRequestedEto evt = new(requestId, Guid.NewGuid(), DateTimeOffset.UtcNow, "EU_GDPR");

        await saga.Start(evt, registry, DefaultOptions(), context, _metrics);

        // ScheduleAsync is an extension method that calls PublishAsync with DeliveryOptions.
        // NSubstitute cannot intercept extension methods, so we verify the underlying PublishAsync call.
        await context.Received(1).PublishAsync(
            Arg.Is<ExportTimedOutEvent>(t => t.RequestId == requestId),
            Arg.Any<DeliveryOptions>());
    }

    [Fact]
    public async Task Handle_PreparedEvent_ReturnsNullUntilAllFragmentsArrived()
    {
        PersonalDataExportSaga saga = new();
        IMessageContext context = Substitute.For<IMessageContext>();
        DataProviderRegistry registry = BuildRegistry("patients", "billing", "appointments");
        PersonalDataRequestedEto startEvt = new(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, "EU_GDPR");
        await saga.Start(startEvt, registry, DefaultOptions(), context, _metrics);

        ExportCompletedEto? result1 = saga.Handle(
            new PersonalDataPreparedEto(startEvt.RequestId, "patients", "blob-1", "application/json"), _metrics);
        ExportCompletedEto? result2 = saga.Handle(
            new PersonalDataPreparedEto(startEvt.RequestId, "billing", "blob-2", "application/json"), _metrics);

        result1.ShouldBeNull();
        result2.ShouldBeNull();
        saga.ReceivedFragments.Count.ShouldBe(2);
        saga.PendingProviders.ShouldContain("appointments");
    }

    [Fact]
    public async Task Handle_PreparedEvent_ReturnsCompletedEvent_WhenAllFragmentsArrived()
    {
        PersonalDataExportSaga saga = new();
        IMessageContext context = Substitute.For<IMessageContext>();
        DataProviderRegistry registry = BuildRegistry("patients", "billing");
        PersonalDataRequestedEto startEvt = new(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, "EU_GDPR");
        await saga.Start(startEvt, registry, DefaultOptions(), context, _metrics);

        saga.Handle(new PersonalDataPreparedEto(startEvt.RequestId, "patients", "blob-patients", "application/json"), _metrics);
        ExportCompletedEto? result = saga.Handle(
            new PersonalDataPreparedEto(startEvt.RequestId, "billing", "blob-billing", "application/json"), _metrics);

        result.ShouldNotBeNull();
        result!.RequestId.ShouldBe(startEvt.RequestId);
        result.UserId.ShouldBe(startEvt.UserId);
        result.IsPartial.ShouldBeFalse();
        result.MissingProviders.ShouldBeEmpty();
        result.ArchiveBlobReferenceId.ShouldBe($"personal-data-export/{startEvt.RequestId}");
        result.Fragments.Count.ShouldBe(2);
        result.Fragments.Select(f => f.BlobReferenceId).ShouldBe(["blob-patients", "blob-billing"], ignoreOrder: true);
    }

    // -------------------------------------------------------------------------
    // Scénario 2 : export partiel avec timeout
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handle_Timeout_ReturnsPartialEvent_WithMissingProviders()
    {
        PersonalDataExportSaga saga = new();
        IMessageContext context = Substitute.For<IMessageContext>();
        DataProviderRegistry registry = BuildRegistry("patients", "billing", "appointments");
        PersonalDataRequestedEto startEvt = new(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, "EU_GDPR");
        await saga.Start(startEvt, registry, DefaultOptions(), context, _metrics);

        saga.Handle(new PersonalDataPreparedEto(startEvt.RequestId, "patients", "blob-patients", "application/json"), _metrics);
        saga.Handle(new PersonalDataPreparedEto(startEvt.RequestId, "billing", "blob-billing", "application/json"), _metrics);
        ExportCompletedEto result = saga.Handle(new ExportTimedOutEvent(startEvt.RequestId), _metrics);

        result.IsPartial.ShouldBeTrue();
        result.MissingProviders.ShouldContain("appointments");
        result.ArchiveBlobReferenceId.ShouldBe($"personal-data-export/{startEvt.RequestId}");
        result.Fragments.Count.ShouldBe(2);
    }

    // -------------------------------------------------------------------------
    // Scénario 3 : aucun provider enregistré
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StartAsync_NoProviders_CompletesImmediately_WithEmptyEvent()
    {
        PersonalDataExportSaga saga = new();
        IMessageContext context = Substitute.For<IMessageContext>();
        DataProviderRegistry emptyRegistry = new();
        PersonalDataRequestedEto evt = new(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, "EU_GDPR");

        ExportCompletedEto? result = await saga.Start(evt, emptyRegistry, DefaultOptions(), context, _metrics);

        result.ShouldNotBeNull();
        result!.IsPartial.ShouldBeFalse();
        result.MissingProviders.ShouldBeEmpty();

        // No timeout scheduled when there are no providers
        await context.DidNotReceive().PublishAsync(
            Arg.Any<ExportTimedOutEvent>(),
            Arg.Any<DeliveryOptions>());
    }

    // -------------------------------------------------------------------------
    // Scénario 4 : conformité ISO 27001 — les events ne transportent que des BlobReferenceId
    // -------------------------------------------------------------------------

    [Fact]
    public void PersonalDataPreparedEto_ContainsOnlyBlobReferenceId_NotRawData()
    {
        // Structural contract: the event record only carries a BlobReferenceId,
        // never raw personal data — enforced by the type definition (ISO 27001 compliance).
        PersonalDataPreparedEto evt = new(
            Guid.NewGuid(), "patients", "blob-ref-123", "application/json");

        evt.BlobReferenceId.ShouldBe("blob-ref-123");

        System.Reflection.PropertyInfo[] properties =
            typeof(PersonalDataPreparedEto).GetProperties();
        string[] allowedProperties =
            ["RequestId", "ProviderName", "BlobReferenceId", "ContentType", "EqualityContract"];
        properties.Select(p => p.Name).ShouldAllBe(name => allowedProperties.Contains(name));
    }

    // -------------------------------------------------------------------------
    // Timeout configuration
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StartAsync_UsesConfiguredTimeout_InScheduledTime()
    {
        PersonalDataExportSaga saga = new();
        IMessageContext context = Substitute.For<IMessageContext>();
        IOptions<GranitPrivacyOptions> options = Microsoft.Extensions.Options.Options.Create(
            new GranitPrivacyOptions { ExportTimeoutMinutes = 10 });
        DataProviderRegistry registry = BuildRegistry("auth");
        PersonalDataRequestedEto evt = new(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, "EU_GDPR");

        await saga.Start(evt, registry, options, context, _metrics);

        // ScheduleAsync(message, TimeSpan) sets ScheduleDelay (relative), not ScheduledTime (absolute).
        await context.Received(1).PublishAsync(
            Arg.Any<ExportTimedOutEvent>(),
            Arg.Is<DeliveryOptions>(o =>
                o.ScheduleDelay.HasValue &&
                o.ScheduleDelay.Value == TimeSpan.FromMinutes(10)));
    }

    // -------------------------------------------------------------------------
    // ArchiveBlobReferenceId convention
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExportCompletedEto_ArchiveBlobReferenceId_UsesGdprExportConvention()
    {
        PersonalDataExportSaga saga = new();
        IMessageContext context = Substitute.For<IMessageContext>();
        DataProviderRegistry registry = BuildRegistry("auth");
        PersonalDataRequestedEto startEvt = new(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, "EU_GDPR");
        await saga.Start(startEvt, registry, DefaultOptions(), context, _metrics);

        ExportCompletedEto? result = saga.Handle(
            new PersonalDataPreparedEto(startEvt.RequestId, "auth", "blob-auth", "application/json"), _metrics);

        result!.ArchiveBlobReferenceId.ShouldBe($"personal-data-export/{startEvt.RequestId}");
    }
}
