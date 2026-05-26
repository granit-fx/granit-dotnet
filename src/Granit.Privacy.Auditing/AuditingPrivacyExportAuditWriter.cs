using System.Globalization;
using Granit.Auditing;
using Granit.Auditing.Domain;
using Granit.Privacy.DataExport.Audit;

namespace Granit.Privacy.Auditing;

/// <summary>
/// <see cref="IPrivacyExportAuditWriter"/> implementation that persists each
/// personal-data-export lifecycle event as an <see cref="AuditEntry"/> via
/// <see cref="IAuditingWriter"/>. Every phase lands in
/// <see cref="AuditCategory.DataAccess"/> — these are subject-data retrieval
/// events under GDPR Art. 15 / Art. 20 — and the lifecycle phase itself goes
/// in the synthetic <c>PrivacyExport</c> entity's <c>Phase</c> property so an
/// investigator can correlate all four events for a single request via the
/// shared <c>EntityId</c>.
/// </summary>
/// <remarks>
/// Lifecycle facts are persisted as a synthetic <see cref="AuditEntityChange"/>
/// with <c>EntityType = "PrivacyExport"</c> and the request id as
/// <c>EntityId</c>, so investigators can correlate all four events for a single
/// export by querying that pair.
/// </remarks>
public sealed class AuditingPrivacyExportAuditWriter(IAuditingWriter auditingWriter)
    : IPrivacyExportAuditWriter
{
    private const string SyntheticEntityType = "PrivacyExport";

    public Task WriteExportRequestedAsync(PrivacyExportRequestedAudit data, CancellationToken cancellationToken) =>
        auditingWriter.WriteAsync(new AuditEntry
        {
            Timestamp = data.Timestamp,
            UserId = data.CallerUserId.ToString("D", CultureInfo.InvariantCulture),
            Category = AuditCategory.DataAccess,
            IpAddress = data.ClientIp,
            UserAgent = data.UserAgent,
            TenantId = data.TenantId,
            CorrelationId = data.CorrelationId,
            EntityChanges = [BuildLifecycleChange(
                data.RequestId,
                AuditChangeType.Created,
                [
                    new AuditPropertyChange { PropertyName = "Phase", NewValue = "requested" },
                    new AuditPropertyChange { PropertyName = "SubjectUserId", NewValue = data.SubjectUserId.ToString("D", CultureInfo.InvariantCulture) },
                    new AuditPropertyChange { PropertyName = "Regulation", NewValue = data.Regulation },
                    new AuditPropertyChange { PropertyName = "ResolvedScopes", NewValue = string.Join(",", data.ResolvedScopes) },
                ])],
        }, cancellationToken);

    public Task WriteFragmentPreparedAsync(PrivacyExportFragmentPreparedAudit data, CancellationToken cancellationToken) =>
        auditingWriter.WriteAsync(new AuditEntry
        {
            Timestamp = data.Timestamp,
            UserId = data.SubjectUserId.ToString("D", CultureInfo.InvariantCulture),
            Category = AuditCategory.DataAccess,
            TenantId = data.TenantId,
            EntityChanges = [BuildLifecycleChange(
                data.RequestId,
                AuditChangeType.Modified,
                [
                    new AuditPropertyChange { PropertyName = "Phase", NewValue = "fragment-prepared" },
                    new AuditPropertyChange { PropertyName = "ProviderName", NewValue = data.ProviderName },
                    new AuditPropertyChange { PropertyName = "FragmentKind", NewValue = data.FragmentKind },
                    new AuditPropertyChange { PropertyName = "EntryPathHash", NewValue = data.EntryPathHash },
                    new AuditPropertyChange { PropertyName = "SizeBytes", NewValue = (data.SizeBytes ?? -1).ToString(CultureInfo.InvariantCulture) },
                ])],
        }, cancellationToken);

    public Task WriteAssemblyStartedAsync(PrivacyExportAssemblyStartedAudit data, CancellationToken cancellationToken) =>
        auditingWriter.WriteAsync(new AuditEntry
        {
            Timestamp = data.Timestamp,
            UserId = data.SubjectUserId.ToString("D", CultureInfo.InvariantCulture),
            Category = AuditCategory.DataAccess,
            TenantId = data.TenantId,
            EntityChanges = [BuildLifecycleChange(
                data.RequestId,
                AuditChangeType.Modified,
                [
                    new AuditPropertyChange { PropertyName = "Phase", NewValue = "assembly-started" },
                    new AuditPropertyChange { PropertyName = "Regulation", NewValue = data.Regulation },
                    new AuditPropertyChange { PropertyName = "ExpectedFragmentCount", NewValue = data.ExpectedFragmentCount.ToString(CultureInfo.InvariantCulture) },
                    new AuditPropertyChange { PropertyName = "IsResumed", NewValue = data.IsResumed ? "true" : "false" },
                ])],
        }, cancellationToken);

    public Task WriteShardCompletedAsync(PrivacyExportShardCompletedAudit data, CancellationToken cancellationToken) =>
        auditingWriter.WriteAsync(new AuditEntry
        {
            Timestamp = data.Timestamp,
            UserId = data.SubjectUserId.ToString("D", CultureInfo.InvariantCulture),
            Category = AuditCategory.DataAccess,
            TenantId = data.TenantId,
            EntityChanges = [BuildLifecycleChange(
                data.RequestId,
                AuditChangeType.Modified,
                [
                    new AuditPropertyChange { PropertyName = "Phase", NewValue = "shard-completed" },
                    new AuditPropertyChange { PropertyName = "ShardIndex", NewValue = data.ShardIndex.ToString(CultureInfo.InvariantCulture) },
                    new AuditPropertyChange { PropertyName = "SizeBytes", NewValue = data.SizeBytes.ToString(CultureInfo.InvariantCulture) },
                    new AuditPropertyChange { PropertyName = "Sha256", NewValue = data.Sha256Hex },
                    new AuditPropertyChange { PropertyName = "DurationMs", NewValue = data.DurationMs.ToString(CultureInfo.InvariantCulture) },
                ])],
        }, cancellationToken);

    public Task WriteExportCompletedAsync(PrivacyExportCompletedAudit data, CancellationToken cancellationToken) =>
        auditingWriter.WriteAsync(new AuditEntry
        {
            Timestamp = data.Timestamp,
            UserId = data.SubjectUserId.ToString("D", CultureInfo.InvariantCulture),
            Category = AuditCategory.DataAccess,
            TenantId = data.TenantId,
            EntityChanges = [BuildLifecycleChange(
                data.RequestId,
                AuditChangeType.Modified,
                [
                    new AuditPropertyChange { PropertyName = "Phase", NewValue = "completed" },
                    new AuditPropertyChange { PropertyName = "Regulation", NewValue = data.Regulation },
                    new AuditPropertyChange { PropertyName = "ShardCount", NewValue = data.ShardCount.ToString(CultureInfo.InvariantCulture) },
                    new AuditPropertyChange { PropertyName = "IsPartial", NewValue = data.IsPartial ? "true" : "false" },
                    new AuditPropertyChange { PropertyName = "AssemblyDurationMs", NewValue = data.AssemblyDurationMs.ToString(CultureInfo.InvariantCulture) },
                ])],
        }, cancellationToken);

    public Task WriteShardDownloadedAsync(PrivacyExportShardDownloadedAudit data, CancellationToken cancellationToken) =>
        auditingWriter.WriteAsync(new AuditEntry
        {
            Timestamp = data.Timestamp,
            UserId = data.SubjectUserId.ToString("D", CultureInfo.InvariantCulture),
            Category = AuditCategory.DataAccess,
            IpAddress = data.ClientIp,
            UserAgent = data.UserAgent,
            TenantId = data.TenantId,
            CorrelationId = data.CorrelationId,
            EntityChanges = [BuildLifecycleChange(
                data.RequestId,
                AuditChangeType.Modified,
                [
                    new AuditPropertyChange { PropertyName = "Phase", NewValue = "shard-downloaded" },
                    new AuditPropertyChange { PropertyName = "ShardIndex", NewValue = data.ShardIndex.ToString(CultureInfo.InvariantCulture) },
                    new AuditPropertyChange { PropertyName = "AuthMethod", NewValue = data.AuthMethod ?? "" },
                ])],
        }, cancellationToken);

    public Task WriteExportFailedAsync(PrivacyExportFailedAudit data, CancellationToken cancellationToken) =>
        auditingWriter.WriteAsync(new AuditEntry
        {
            Timestamp = data.Timestamp,
            UserId = data.SubjectUserId.ToString("D", CultureInfo.InvariantCulture),
            Category = AuditCategory.DataAccess,
            TenantId = data.TenantId,
            EntityChanges = [BuildLifecycleChange(
                data.RequestId,
                AuditChangeType.Modified,
                [
                    new AuditPropertyChange { PropertyName = "Phase", NewValue = "failed" },
                    new AuditPropertyChange { PropertyName = "ExceptionType", NewValue = data.ExceptionType },
                    new AuditPropertyChange { PropertyName = "RetryCount", NewValue = data.RetryCount.ToString(CultureInfo.InvariantCulture) },
                ])],
        }, cancellationToken);

    private static AuditEntityChange BuildLifecycleChange(
        Guid requestId,
        AuditChangeType changeType,
        List<AuditPropertyChange> properties) =>
        new()
        {
            EntityType = SyntheticEntityType,
            EntityId = requestId.ToString("D", CultureInfo.InvariantCulture),
            ChangeType = changeType,
            PropertyChanges = properties,
        };
}
