using Granit.Auditing;
using Granit.Modularity;
using Granit.Privacy.Auditing.Extensions;

namespace Granit.Privacy.Auditing;

/// <summary>
/// Granit module wiring personal-data-export lifecycle events to
/// <c>Granit.Auditing</c>. Registering this module replaces the no-op
/// <c>NullPrivacyExportAuditWriter</c> from <c>Granit.Privacy</c> with the
/// <see cref="AuditingPrivacyExportAuditWriter"/>, which persists every
/// requested / completed / shard-downloaded / failed event as an audit row.
/// </summary>
[DependsOn(typeof(GranitAuditingModule))]
[DependsOn(typeof(GranitPrivacyModule))]
public sealed class GranitPrivacyAuditingModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitPrivacyExportAuditing();
}
