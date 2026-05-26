using Granit.Auditing;
using Granit.Auditing.Domain;
using Granit.Privacy.DataExport.Audit;
using NSubstitute;
using Xunit;

namespace Granit.Privacy.Auditing.Tests;

public sealed class AuditingPrivacyExportAuditWriterTests
{
    private static readonly Guid RequestId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CallerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SubjectId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid TenantId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly DateTimeOffset Now = new(2026, 5, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task WriteExportRequestedAsync_PersistsAuditEntryWithRequestedPhase()
    {
        IAuditingWriter writer = Substitute.For<IAuditingWriter>();
        AuditingPrivacyExportAuditWriter sut = new(writer);

        await sut.WriteExportRequestedAsync(new PrivacyExportRequestedAudit(
            RequestId, CallerId, SubjectId, TenantId, "EU_GDPR",
            ResolvedScopes: ["identity", "auditing"],
            ClientIp: "203.0.113.0",
            UserAgent: "Mozilla/5.0",
            CorrelationId: "trace-abc",
            Timestamp: Now), TestContext.Current.CancellationToken);

        await writer.Received(1).WriteAsync(Arg.Is<AuditEntry>(e =>
            e.Category == AuditCategory.DataAccess
            && e.UserId == CallerId.ToString("D")
            && e.TenantId == TenantId
            && e.IpAddress == "203.0.113.0"
            && e.UserAgent == "Mozilla/5.0"
            && e.CorrelationId == "trace-abc"
            && e.EntityChanges.Count == 1
            && e.EntityChanges.First().EntityType == "PrivacyExport"
            && e.EntityChanges.First().EntityId == RequestId.ToString("D")
            && e.EntityChanges.First().ChangeType == AuditChangeType.Created
            && e.EntityChanges.First().PropertyChanges.Any(p => p.PropertyName == "Phase" && p.NewValue == "requested")
            && e.EntityChanges.First().PropertyChanges.Any(p => p.PropertyName == "ResolvedScopes" && p.NewValue == "identity,auditing")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteExportCompletedAsync_PersistsCompletedPhaseWithShardCount()
    {
        IAuditingWriter writer = Substitute.For<IAuditingWriter>();
        AuditingPrivacyExportAuditWriter sut = new(writer);

        await sut.WriteExportCompletedAsync(new PrivacyExportCompletedAudit(
            RequestId, SubjectId, TenantId, "EU_GDPR",
            ShardCount: 3, IsPartial: false, AssemblyDurationMs: 1234, Timestamp: Now),
            TestContext.Current.CancellationToken);

        await writer.Received(1).WriteAsync(Arg.Is<AuditEntry>(e =>
            e.Category == AuditCategory.DataAccess
            && e.UserId == SubjectId.ToString("D")
            && e.EntityChanges.First().ChangeType == AuditChangeType.Modified
            && e.EntityChanges.First().PropertyChanges.Any(p => p.PropertyName == "Phase" && p.NewValue == "completed")
            && e.EntityChanges.First().PropertyChanges.Any(p => p.PropertyName == "ShardCount" && p.NewValue == "3")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteShardDownloadedAsync_PersistsShardIndexAndAuthMethod()
    {
        IAuditingWriter writer = Substitute.For<IAuditingWriter>();
        AuditingPrivacyExportAuditWriter sut = new(writer);

        await sut.WriteShardDownloadedAsync(new PrivacyExportShardDownloadedAudit(
            RequestId, SubjectId, TenantId,
            ShardIndex: 2,
            ClientIp: "203.0.113.0",
            UserAgent: "Mozilla/5.0",
            AuthMethod: "Bearer",
            CorrelationId: "trace-xyz",
            Timestamp: Now), TestContext.Current.CancellationToken);

        await writer.Received(1).WriteAsync(Arg.Is<AuditEntry>(e =>
            e.Category == AuditCategory.DataAccess
            && e.EntityChanges.First().PropertyChanges.Any(p => p.PropertyName == "ShardIndex" && p.NewValue == "2")
            && e.EntityChanges.First().PropertyChanges.Any(p => p.PropertyName == "AuthMethod" && p.NewValue == "Bearer")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteFragmentPreparedAsync_PersistsFragmentPhaseWithHashedEntryPath()
    {
        IAuditingWriter writer = Substitute.For<IAuditingWriter>();
        AuditingPrivacyExportAuditWriter sut = new(writer);

        await sut.WriteFragmentPreparedAsync(new PrivacyExportFragmentPreparedAudit(
            RequestId, SubjectId, TenantId,
            ProviderName: "identity-local",
            FragmentKind: "staged",
            EntryPathHash: "abc123",
            SizeBytes: 4096,
            Timestamp: Now), TestContext.Current.CancellationToken);

        await writer.Received(1).WriteAsync(Arg.Is<AuditEntry>(e =>
            e.Category == AuditCategory.DataAccess
            && e.EntityChanges.First().PropertyChanges.Any(p => p.PropertyName == "Phase" && p.NewValue == "fragment-prepared")
            && e.EntityChanges.First().PropertyChanges.Any(p => p.PropertyName == "ProviderName" && p.NewValue == "identity-local")
            && e.EntityChanges.First().PropertyChanges.Any(p => p.PropertyName == "EntryPathHash" && p.NewValue == "abc123")
            && e.EntityChanges.First().PropertyChanges.Any(p => p.PropertyName == "SizeBytes" && p.NewValue == "4096")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteAssemblyStartedAsync_PersistsAssemblyStartedPhase()
    {
        IAuditingWriter writer = Substitute.For<IAuditingWriter>();
        AuditingPrivacyExportAuditWriter sut = new(writer);

        await sut.WriteAssemblyStartedAsync(new PrivacyExportAssemblyStartedAudit(
            RequestId, SubjectId, TenantId, "EU_GDPR",
            ExpectedFragmentCount: 5,
            IsResumed: true,
            Timestamp: Now), TestContext.Current.CancellationToken);

        await writer.Received(1).WriteAsync(Arg.Is<AuditEntry>(e =>
            e.EntityChanges.First().PropertyChanges.Any(p => p.PropertyName == "Phase" && p.NewValue == "assembly-started")
            && e.EntityChanges.First().PropertyChanges.Any(p => p.PropertyName == "ExpectedFragmentCount" && p.NewValue == "5")
            && e.EntityChanges.First().PropertyChanges.Any(p => p.PropertyName == "IsResumed" && p.NewValue == "true")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteShardCompletedAsync_PersistsShardCompletedPhaseWithSha256()
    {
        IAuditingWriter writer = Substitute.For<IAuditingWriter>();
        AuditingPrivacyExportAuditWriter sut = new(writer);

        await sut.WriteShardCompletedAsync(new PrivacyExportShardCompletedAudit(
            RequestId, SubjectId, TenantId,
            ShardIndex: 1,
            SizeBytes: 1024 * 1024,
            Sha256Hex: "deadbeef",
            DurationMs: 9876,
            Timestamp: Now), TestContext.Current.CancellationToken);

        await writer.Received(1).WriteAsync(Arg.Is<AuditEntry>(e =>
            e.EntityChanges.First().PropertyChanges.Any(p => p.PropertyName == "Phase" && p.NewValue == "shard-completed")
            && e.EntityChanges.First().PropertyChanges.Any(p => p.PropertyName == "ShardIndex" && p.NewValue == "1")
            && e.EntityChanges.First().PropertyChanges.Any(p => p.PropertyName == "Sha256" && p.NewValue == "deadbeef")
            && e.EntityChanges.First().PropertyChanges.Any(p => p.PropertyName == "DurationMs" && p.NewValue == "9876")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteExportFailedAsync_PersistsFailedPhaseWithoutPii()
    {
        IAuditingWriter writer = Substitute.For<IAuditingWriter>();
        AuditingPrivacyExportAuditWriter sut = new(writer);

        await sut.WriteExportFailedAsync(new PrivacyExportFailedAudit(
            RequestId, SubjectId, TenantId,
            ExceptionType: "PrivacyExportAssemblyException",
            RetryCount: 3,
            Timestamp: Now), TestContext.Current.CancellationToken);

        await writer.Received(1).WriteAsync(Arg.Is<AuditEntry>(e =>
            e.Category == AuditCategory.DataAccess
            && e.IpAddress == null
            && e.UserAgent == null
            && e.EntityChanges.First().PropertyChanges.Any(p => p.PropertyName == "Phase" && p.NewValue == "failed")
            && e.EntityChanges.First().PropertyChanges.Any(p => p.PropertyName == "ExceptionType" && p.NewValue == "PrivacyExportAssemblyException")
            && e.EntityChanges.First().PropertyChanges.Any(p => p.PropertyName == "RetryCount" && p.NewValue == "3")),
            Arg.Any<CancellationToken>());
    }
}
