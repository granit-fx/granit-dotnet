using Granit.Auditing.Privacy.DataExport;
using Granit.Modularity;
using Granit.Privacy.BlobStorage;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Auditing.Privacy;

/// <summary>
/// Granit module that registers the <see cref="AuditingPrivacyDataProvider"/> so the audit
/// trail participates in the privacy export scatter-gather saga (GDPR Art. 15).
/// </summary>
/// <remarks>
/// Two Wolverine handlers are discovered automatically by assembly scanning:
/// <see cref="AuditingPersonalDataExportHandler"/> (Art. 15 export of the subject's audit entries)
/// and <c>AuditingPersonalDataDeletionHandler</c> (Art. 17 fan-in). Because auditing registers as a
/// privacy provider, the deletion saga waits on its acknowledgement — the deletion handler emits a
/// <c>Retained</c> acknowledgement (the audit trail is intentionally never erased) so the saga can
/// reach a terminal state. Apps opt in via
/// <c>AddGranitPrivacy(p =&gt; p.AddGranitAuditingPrivacyProvider())</c> to register the
/// provider with the scatter-gather registry.
/// </remarks>
[DependsOn(
    typeof(GranitAuditingModule),
    typeof(GranitPrivacyBlobStorageModule))]
public sealed class GranitAuditingPrivacyModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.TryAddScoped<AuditingPrivacyDataProvider>();
}
