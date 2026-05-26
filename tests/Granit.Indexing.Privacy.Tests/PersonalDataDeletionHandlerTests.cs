using Granit.MultiTenancy;
using Granit.Privacy.DataDeletion.Events;
using Shouldly;
using Xunit;

namespace Granit.Indexing.Privacy.Tests;

public sealed class PersonalDataDeletionHandlerTests
{
    [Fact]
    public async Task Calls_every_registered_eraser_with_event_subject_and_tenant()
    {
        var subject = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        FakeEraser ef = new("ef_tsvector");
        FakeEraser es = new("elasticsearch");
        PersonalDataDeletionRequestedEto @event = new(
            RequestId: Guid.NewGuid(),
            UserId: subject,
            RequestedBy: "user@example.com",
            RequestedAt: DateTimeOffset.UtcNow,
            Reason: "GDPR",
            Regulation: "GDPR",
            TenantId: tenantId);

        await PersonalDataDeletionHandler.Handle(
            @event,
            [ef, es],
            NullTenantContext.Instance,
            TestContext.Current.CancellationToken);

        ef.Calls.ShouldBe([(tenantId, subject)]);
        es.Calls.ShouldBe([(tenantId, subject)]);
    }

    [Fact]
    public async Task Falls_back_to_current_tenant_when_event_tenant_id_is_null()
    {
        var currentTenantId = Guid.NewGuid();
        StubCurrentTenant tenant = new(currentTenantId);
        FakeEraser ef = new("ef_tsvector");

        PersonalDataDeletionRequestedEto @event = new(
            RequestId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            RequestedBy: "u",
            RequestedAt: DateTimeOffset.UtcNow,
            Reason: "r",
            Regulation: "GDPR",
            TenantId: null);

        await PersonalDataDeletionHandler.Handle(@event, [ef], tenant, TestContext.Current.CancellationToken);

        ef.Calls[0].TenantId.ShouldBe(currentTenantId);
    }

    [Fact]
    public async Task Does_not_swallow_exceptions_from_erasers()
    {
        FailingEraser failing = new();
        FakeEraser ok = new("ok");

        PersonalDataDeletionRequestedEto @event = new(
            RequestId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            RequestedBy: "u",
            RequestedAt: DateTimeOffset.UtcNow,
            Reason: "r",
            Regulation: "GDPR");

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await PersonalDataDeletionHandler.Handle(@event, [failing, ok], NullTenantContext.Instance, TestContext.Current.CancellationToken));

        ex.Message.ShouldBe("boom");
    }

    private sealed class FakeEraser(string name) : IIndexedDataEraser
    {
        public string Name { get; } = name;
        public List<(Guid? TenantId, Guid SubjectId)> Calls { get; } = [];

        public Task<int> EraseAsync(Guid? tenantId, Guid dataSubjectId, CancellationToken cancellationToken = default)
        {
            Calls.Add((tenantId, dataSubjectId));
            return Task.FromResult(1);
        }
    }

    private sealed class FailingEraser : IIndexedDataEraser
    {
        public string Name => "failing";

        public Task<int> EraseAsync(Guid? tenantId, Guid dataSubjectId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("boom");
    }

    private sealed class StubCurrentTenant(Guid? id) : ICurrentTenant
    {
        public bool IsAvailable => id is not null;
        public Guid? Id => id;
        public string? Name => null;
        public IDisposable Change(Guid? id, string? name = null) => Empty.Instance;

        private sealed class Empty : IDisposable
        {
            public static readonly Empty Instance = new();
            public void Dispose() { }
        }
    }
}
