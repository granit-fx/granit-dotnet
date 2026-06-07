using System.Diagnostics.Metrics;
using Granit.Domain.ValueObjects;
using Granit.MultiTenancy;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Events;
using Granit.Privacy.Diagnostics;
using Granit.Privacy.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Wolverine;
using Xunit;

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

    private static readonly ICurrentTenant CurrentTenant = NullTenantContext.Instance;

    private static IOptions<GranitPrivacyOptions> DefaultOptions() =>
        Microsoft.Extensions.Options.Options.Create(new GranitPrivacyOptions { ExportTimeoutMinutes = 5 });

    private static IPrivacyScopeResolver BuildScopeResolver(params string[] providers)
    {
        IReadOnlyList<ProviderDescriptor> descriptors = [.. providers.Select(name =>
            new ProviderDescriptor(
                ProviderName: name,
                DisplayKey: $"Privacy.Scopes.{name}",
                FeatureName: null))];

        IPrivacyScopeResolver resolver = Substitute.For<IPrivacyScopeResolver>();
        resolver.ListVisibleAsync(Arg.Any<PrivacyExportContext>(), Arg.Any<CancellationToken>())
            .Returns(descriptors);
        return resolver;
    }

    private static PersonalDataPreparedEto StagedFragmentEto(
        Guid requestId, string providerName, string blobValue, string entryPath) =>
        new(
            RequestId: requestId,
            ProviderName: providerName,
            FragmentKind: "staged",
            SourceContainer: "gdpr-exports",
            BlobReferenceId: BlobReference.Create(blobValue),
            EntryPath: entryPath,
            ContentType: "application/json",
            IntegrityTag: "v1:test");

    // -------------------------------------------------------------------------
    // Scénario 1 : export complet avec plusieurs modules
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StartAsync_InitializesState_FromEvent()
    {
        PersonalDataExportSaga saga = new();
        IMessageContext context = Substitute.For<IMessageContext>();
        IPrivacyScopeResolver scopeResolver = BuildScopeResolver("patients", "billing");
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        PersonalDataRequestedEto evt = new(requestId, userId, DateTimeOffset.UtcNow, "EU_GDPR");

        await saga.Start(evt, scopeResolver, CurrentTenant, DefaultOptions(), context, _metrics, TestContext.Current.CancellationToken);

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
        IPrivacyScopeResolver scopeResolver = BuildScopeResolver("patients", "billing");
        var requestId = Guid.NewGuid();
        PersonalDataRequestedEto evt = new(requestId, Guid.NewGuid(), DateTimeOffset.UtcNow, "EU_GDPR");

        await saga.Start(evt, scopeResolver, CurrentTenant, DefaultOptions(), context, _metrics, TestContext.Current.CancellationToken);

        // ScheduleAsync is an extension method that calls PublishAsync with DeliveryOptions.
        // NSubstitute cannot intercept extension methods, so we verify the underlying PublishAsync call.
        await context.Received(1).PublishAsync(
            Arg.Is<ExportTimedOutEvent>(t => t.RequestId == requestId),
            Arg.Any<DeliveryOptions>());
    }

    [Fact]
    public async Task Handle_PreparedEvent_ReturnsNullUntilAllProvidersResponded()
    {
        PersonalDataExportSaga saga = new();
        IMessageContext context = Substitute.For<IMessageContext>();
        IPrivacyScopeResolver scopeResolver = BuildScopeResolver("patients", "billing", "appointments");
        PersonalDataRequestedEto startEvt = new(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, "EU_GDPR");
        await saga.Start(startEvt, scopeResolver, CurrentTenant, DefaultOptions(), context, _metrics, TestContext.Current.CancellationToken);

        ExportCompletedEto? result1 = saga.Handle(
            StagedFragmentEto(startEvt.RequestId, "patients", "blob-1", "patients.json"), _metrics);
        ExportCompletedEto? result2 = saga.Handle(
            StagedFragmentEto(startEvt.RequestId, "billing", "blob-2", "billing.json"), _metrics);

        result1.ShouldBeNull();
        result2.ShouldBeNull();
        saga.ReceivedFragments.Count.ShouldBe(2);
        saga.PendingProviders.ShouldContain("appointments");
    }

    [Fact]
    public async Task Handle_PreparedEvent_ReturnsCompletedEvent_WhenAllProvidersResponded()
    {
        PersonalDataExportSaga saga = new();
        IMessageContext context = Substitute.For<IMessageContext>();
        IPrivacyScopeResolver scopeResolver = BuildScopeResolver("patients", "billing");
        PersonalDataRequestedEto startEvt = new(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, "EU_GDPR");
        await saga.Start(startEvt, scopeResolver, CurrentTenant, DefaultOptions(), context, _metrics, TestContext.Current.CancellationToken);

        saga.Handle(StagedFragmentEto(startEvt.RequestId, "patients", "blob-patients", "patients.json"), _metrics);
        ExportCompletedEto? result = saga.Handle(
            StagedFragmentEto(startEvt.RequestId, "billing", "blob-billing", "billing.json"), _metrics);

        result.ShouldNotBeNull();
        result!.RequestId.ShouldBe(startEvt.RequestId);
        result.UserId.ShouldBe(startEvt.UserId);
        result.IsPartial.ShouldBeFalse();
        result.MissingProviders.ShouldBeEmpty();
        result.ArchiveBlobReferenceId.Value.ShouldBe($"personal-data-export/{startEvt.RequestId}");
        result.Fragments.Count.ShouldBe(2);
        result.Fragments.Select(f => f.BlobReferenceId.Value).ShouldBe(["blob-patients", "blob-billing"], ignoreOrder: true);
    }

    [Fact]
    public async Task Handle_PreparedEvent_AcceptsMultipleFragmentsFromSingleProvider()
    {
        // P6.1 multi-fragment-friendly: a provider (Documents-like) can emit N
        // PersonalDataPreparedEto events; the saga only deducts from PendingProviders once.
        PersonalDataExportSaga saga = new();
        IMessageContext context = Substitute.For<IMessageContext>();
        IPrivacyScopeResolver scopeResolver = BuildScopeResolver("documents");
        PersonalDataRequestedEto startEvt = new(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, "EU_GDPR");
        await saga.Start(startEvt, scopeResolver, CurrentTenant, DefaultOptions(), context, _metrics, TestContext.Current.CancellationToken);

        saga.Handle(StagedFragmentEto(startEvt.RequestId, "documents", "blob-1", "Documents/a.pdf"), _metrics);
        ExportCompletedEto? second = saga.Handle(StagedFragmentEto(startEvt.RequestId, "documents", "blob-2", "Documents/b.pdf"), _metrics);
        ExportCompletedEto? third = saga.Handle(StagedFragmentEto(startEvt.RequestId, "documents", "blob-3", "Documents/c.pdf"), _metrics);

        // First fragment from "documents" emptied PendingProviders → saga completes there.
        // Subsequent fragments still get recorded; completion fires every time PendingProviders is empty,
        // which is acceptable since Wolverine will dedupe by saga identity.
        saga.ReceivedFragments.Count.ShouldBe(3);
        saga.PendingProviders.ShouldBeEmpty();
        second.ShouldNotBeNull();
        third.ShouldNotBeNull();
    }

    // -------------------------------------------------------------------------
    // Scénario 2 : export partiel avec timeout
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handle_Timeout_ReturnsPartialEvent_WithMissingProviders()
    {
        PersonalDataExportSaga saga = new();
        IMessageContext context = Substitute.For<IMessageContext>();
        IPrivacyScopeResolver scopeResolver = BuildScopeResolver("patients", "billing", "appointments");
        PersonalDataRequestedEto startEvt = new(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, "EU_GDPR");
        await saga.Start(startEvt, scopeResolver, CurrentTenant, DefaultOptions(), context, _metrics, TestContext.Current.CancellationToken);

        saga.Handle(StagedFragmentEto(startEvt.RequestId, "patients", "blob-patients", "patients.json"), _metrics);
        saga.Handle(StagedFragmentEto(startEvt.RequestId, "billing", "blob-billing", "billing.json"), _metrics);
        ExportCompletedEto result = saga.Handle(new ExportTimedOutEvent(startEvt.RequestId), _metrics);

        result.IsPartial.ShouldBeTrue();
        result.MissingProviders.ShouldContain("appointments");
        result.ArchiveBlobReferenceId.Value.ShouldBe($"personal-data-export/{startEvt.RequestId}");
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
        PersonalDataRequestedEto evt = new(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, "EU_GDPR");

        // No providers registered → resolver returns empty list, saga completes immediately.
        ExportCompletedEto? result = await saga.Start(evt, BuildScopeResolver(), CurrentTenant, DefaultOptions(), context, _metrics, TestContext.Current.CancellationToken);

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
        // Structural contract: the event record only carries fragment metadata and a
        // BlobReferenceId, never raw personal data — enforced by the type definition.
        PersonalDataPreparedEto evt = StagedFragmentEto(Guid.NewGuid(), "patients", "blob-ref-123", "patients.json");

        evt.BlobReferenceId.Value.ShouldBe("blob-ref-123");

        System.Reflection.PropertyInfo[] properties =
            typeof(PersonalDataPreparedEto).GetProperties();
        string[] allowedProperties =
        [
            "RequestId", "ProviderName", "FragmentKind", "SourceContainer",
            "BlobReferenceId", "EntryPath", "ContentType", "IntegrityTag",
            "TenantId", "EqualityContract",
        ];
        properties.Select(p => p.Name).ShouldAllBe(name => allowedProperties.Contains(name));
    }

    // -------------------------------------------------------------------------
    // TenantId propagation (saga state → ExportCompletedEto)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Start_StoresTenantIdOnSagaState_FromIncomingEto()
    {
        PersonalDataExportSaga saga = new();
        IMessageContext context = Substitute.For<IMessageContext>();
        IPrivacyScopeResolver scopeResolver = BuildScopeResolver("identity");
        var tenantId = Guid.NewGuid();
        PersonalDataRequestedEto evt = new(
            Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, "EU_GDPR", TenantId: tenantId);

        await saga.Start(evt, scopeResolver, CurrentTenant, DefaultOptions(), context, _metrics, TestContext.Current.CancellationToken);

        saga.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public async Task Start_PropagatesTenantIdToExportCompletedEto_WhenNoProviders()
    {
        PersonalDataExportSaga saga = new();
        IMessageContext context = Substitute.For<IMessageContext>();
        var tenantId = Guid.NewGuid();
        PersonalDataRequestedEto evt = new(
            Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, "EU_GDPR", TenantId: tenantId);

        ExportCompletedEto? result = await saga.Start(evt, BuildScopeResolver(), CurrentTenant, DefaultOptions(), context, _metrics, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result!.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public async Task HandleTimeout_PropagatesTenantIdToExportCompletedEto()
    {
        PersonalDataExportSaga saga = new();
        IMessageContext context = Substitute.For<IMessageContext>();
        IPrivacyScopeResolver scopeResolver = BuildScopeResolver("auth");
        var tenantId = Guid.NewGuid();
        PersonalDataRequestedEto startEvt = new(
            Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, "EU_GDPR", TenantId: tenantId);
        await saga.Start(startEvt, scopeResolver, CurrentTenant, DefaultOptions(), context, _metrics, TestContext.Current.CancellationToken);

        ExportCompletedEto result = saga.Handle(new ExportTimedOutEvent(startEvt.RequestId), _metrics);

        result.TenantId.ShouldBe(tenantId);
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
        IPrivacyScopeResolver scopeResolver = BuildScopeResolver("auth");
        PersonalDataRequestedEto evt = new(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, "EU_GDPR");

        await saga.Start(evt, scopeResolver, CurrentTenant, options, context, _metrics, TestContext.Current.CancellationToken);

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
        IPrivacyScopeResolver scopeResolver = BuildScopeResolver("auth");
        PersonalDataRequestedEto startEvt = new(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, "EU_GDPR");
        await saga.Start(startEvt, scopeResolver, CurrentTenant, DefaultOptions(), context, _metrics, TestContext.Current.CancellationToken);

        ExportCompletedEto? result = saga.Handle(
            StagedFragmentEto(startEvt.RequestId, "auth", "blob-auth", "auth.json"), _metrics);

        result!.ArchiveBlobReferenceId.Value.ShouldBe($"personal-data-export/{startEvt.RequestId}");
    }

    // -------------------------------------------------------------------------
    // Tenant scope anchor — regression for the 42P01 bug when the envelope reaches
    // the saga without TenantContextBehavior having set ICurrentTenant (outbox
    // replay, saga rehydration, local-queue path, inline test invocation).
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Start_AnchorsTenantFromEvent_BeforeResolvingProviders()
    {
        // Arrange: ICurrentTenant starts inactive — simulates the envelope reaching
        // the saga without an X-Tenant-Id header restored by TenantContextBehavior.
        RecordingCurrentTenant currentTenant = new();
        Guid? observedDuringProbe = null;

        IPrivacyScopeResolver scopeResolver = Substitute.For<IPrivacyScopeResolver>();
        scopeResolver.ListVisibleAsync(Arg.Any<PrivacyExportContext>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                observedDuringProbe = currentTenant.Id;
                return (IReadOnlyList<ProviderDescriptor>)[];
            });

        PersonalDataExportSaga saga = new();
        IMessageContext context = Substitute.For<IMessageContext>();
        var tenantId = Guid.NewGuid();
        PersonalDataRequestedEto evt = new(
            Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, "EU_GDPR", TenantId: tenantId);

        currentTenant.Id.ShouldBeNull("baseline: no tenant active before saga.Start");

        // Act
        await saga.Start(evt, scopeResolver, currentTenant, DefaultOptions(), context, _metrics, TestContext.Current.CancellationToken);

        // Assert: the scope was open during the scope-resolver invocation (i.e. before
        // any tenant-isolated DbContext could be resolved by a provider's HasDataAsync)…
        observedDuringProbe.ShouldBe(tenantId);

        // …and restored after the saga method returns.
        currentTenant.Id.ShouldBeNull();
    }

    private sealed class RecordingCurrentTenant : ICurrentTenant
    {
        public bool IsAvailable => Id.HasValue;
        public Guid? Id { get; private set; }
        public string? Name { get; private set; }
        public string? Jurisdiction { get; private set; }

        public IDisposable Change(Guid? id, string? name = null, string? jurisdiction = null)
        {
            (Guid? previousId, string? previousName, string? previousJurisdiction) = (Id, Name, Jurisdiction);
            Id = id;
            Name = name;
            Jurisdiction = jurisdiction;
            return new Restore(this, previousId, previousName, previousJurisdiction);
        }

        private sealed class Restore(RecordingCurrentTenant owner, Guid? previousId, string? previousName, string? previousJurisdiction) : IDisposable
        {
            public void Dispose()
            {
                owner.Id = previousId;
                owner.Name = previousName;
                owner.Jurisdiction = previousJurisdiction;
            }
        }
    }
}
