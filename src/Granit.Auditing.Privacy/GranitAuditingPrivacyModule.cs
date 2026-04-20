using Granit.Auditing.Privacy.DataExport;
using Granit.Modularity;
using Granit.Privacy.BlobStorage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Auditing.Privacy;

/// <summary>
/// Granit module that registers the <see cref="AuditingPrivacyDataProvider"/> so the audit
/// trail participates in the privacy export scatter-gather saga (GDPR Art. 15).
/// </summary>
/// <remarks>
/// The matching Wolverine handler (<see cref="AuditingPersonalDataExportHandler"/>) is
/// discovered automatically by assembly scanning. Apps opt in via
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
