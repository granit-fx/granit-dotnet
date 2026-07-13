using System.Diagnostics.Metrics;
using Granit.Events;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Audit;
using Granit.Privacy.DataExport.Events;
using Granit.Privacy.DataExport.Internal;
using Granit.Privacy.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Tests.DataExport.Internal;

public sealed class PrivacyExportRequestServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid RequestId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Subject = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Caller = new("33333333-3333-3333-3333-333333333333");

    private readonly ServiceProvider _sp;
    private readonly PrivacyMetrics _metrics;
    private readonly IDistributedEventBus _eventBus = Substitute.For<IDistributedEventBus>();
    private readonly IPrivacyExportAuditWriter _audit = Substitute.For<IPrivacyExportAuditWriter>();
    private readonly IPrivacySubjectValidator _subjectValidator = Substitute.For<IPrivacySubjectValidator>();
    private readonly IExportRequestTrackerWriter _tracker = Substitute.For<IExportRequestTrackerWriter>();
    private readonly IGuidGenerator _guids = Substitute.For<IGuidGenerator>();
    private readonly TimeProvider _time = Substitute.For<TimeProvider>();

    private static readonly ExportRequestAuditMetadata Audit = new("203.0.113.0", "agent", "corr-1");

    public PrivacyExportRequestServiceTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _metrics = new PrivacyMetrics(_sp.GetRequiredService<IMeterFactory>());
        _guids.Create().Returns(RequestId);
        _time.GetUtcNow().Returns(Now);
    }

    public void Dispose() => _sp.Dispose();

    private PrivacyExportRequestService CreateSut() =>
        new(_eventBus, _audit, _subjectValidator, _metrics, _time, NullTenantContext.Instance, _guids, _tracker);

    [Fact]
    public async Task RequestExportAsync_SelfService_RecordsPublishesAndAudits()
    {
        PrivacyExportRequestService sut = CreateSut();

        RequestExportOutcome outcome = await sut.RequestExportAsync(
            new RequestExportCommand(Subject, Subject, Scopes: null, "EU_GDPR", ValidateSubject: false, Audit),
            TestContext.Current.CancellationToken);

        outcome.Result.ShouldBe(RequestExportResult.Accepted);
        outcome.RequestId.ShouldBe(RequestId);
        outcome.RequestedAt.ShouldBe(Now);
        await _tracker.Received(1).RecordRequestAsync(RequestId, Subject, Subject, Now, Arg.Any<CancellationToken>());
        await _eventBus.Received(1).PublishAsync(Arg.Any<PersonalDataRequestedEto>(), Arg.Any<CancellationToken>());
        await _audit.Received(1).WriteExportRequestedAsync(
            Arg.Is<PrivacyExportRequestedAudit>(a => a.RequestId == RequestId && a.ClientIp == "203.0.113.0"),
            Arg.Any<CancellationToken>());
        await _subjectValidator.DidNotReceive().SubjectExistsInCurrentTenantAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestExportAsync_OnBehalfOf_MissingSubject_ReturnsNotFoundAndDoesNothing()
    {
        _subjectValidator.SubjectExistsInCurrentTenantAsync(Subject, Arg.Any<CancellationToken>()).Returns(false);
        PrivacyExportRequestService sut = CreateSut();

        RequestExportOutcome outcome = await sut.RequestExportAsync(
            new RequestExportCommand(Subject, Caller, Scopes: ["profile"], "EU_GDPR", ValidateSubject: true, Audit),
            TestContext.Current.CancellationToken);

        outcome.Result.ShouldBe(RequestExportResult.SubjectNotFound);
        await _tracker.DidNotReceive().RecordRequestAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<PersonalDataRequestedEto>(), Arg.Any<CancellationToken>());
        await _audit.DidNotReceive().WriteExportRequestedAsync(Arg.Any<PrivacyExportRequestedAudit>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestExportAsync_OnBehalfOf_ExistingSubject_RecordsCallerDistinctFromSubject()
    {
        _subjectValidator.SubjectExistsInCurrentTenantAsync(Subject, Arg.Any<CancellationToken>()).Returns(true);
        PrivacyExportRequestService sut = CreateSut();

        RequestExportOutcome outcome = await sut.RequestExportAsync(
            new RequestExportCommand(Subject, Caller, Scopes: null, "EU_GDPR", ValidateSubject: true, Audit),
            TestContext.Current.CancellationToken);

        outcome.Result.ShouldBe(RequestExportResult.Accepted);
        await _tracker.Received(1).RecordRequestAsync(RequestId, Subject, Caller, Now, Arg.Any<CancellationToken>());
        await _audit.Received(1).WriteExportRequestedAsync(
            Arg.Is<PrivacyExportRequestedAudit>(a => a.SubjectUserId == Subject && a.CallerUserId == Caller),
            Arg.Any<CancellationToken>());
    }
}
