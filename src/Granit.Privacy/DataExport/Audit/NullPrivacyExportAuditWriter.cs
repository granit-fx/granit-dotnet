namespace Granit.Privacy.DataExport.Audit;

/// <summary>
/// No-op default for hosts that don't ship an audit trail. The DI registration in
/// <c>Granit.Privacy</c> wires this so the export path never has to null-check the
/// writer — hosts that do want ROPA inscription reference
/// <c>Granit.Privacy.Auditing</c>, which registers a real implementation on top
/// of <c>IAuditingWriter</c> and bumps this null one out via <c>Replace</c>.
/// </summary>
internal sealed class NullPrivacyExportAuditWriter : IPrivacyExportAuditWriter
{
    public Task WriteExportRequestedAsync(PrivacyExportRequestedAudit data, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task WriteFragmentPreparedAsync(PrivacyExportFragmentPreparedAudit data, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task WriteAssemblyStartedAsync(PrivacyExportAssemblyStartedAudit data, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task WriteShardCompletedAsync(PrivacyExportShardCompletedAudit data, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task WriteExportCompletedAsync(PrivacyExportCompletedAudit data, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task WriteShardDownloadedAsync(PrivacyExportShardDownloadedAudit data, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task WriteExportFailedAsync(PrivacyExportFailedAudit data, CancellationToken cancellationToken) => Task.CompletedTask;
}
