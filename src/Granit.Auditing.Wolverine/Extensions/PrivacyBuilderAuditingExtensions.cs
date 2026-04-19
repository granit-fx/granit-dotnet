using Granit.Auditing.Wolverine.DataExport;
using Granit.Privacy;

namespace Granit.Auditing.Wolverine.Extensions;

/// <summary>
/// <see cref="GranitPrivacyBuilder"/> extensions that register the auditing privacy provider.
/// </summary>
public static class PrivacyBuilderAuditingExtensions
{
    /// <summary>
    /// Registers <see cref="AuditingPrivacyDataProvider"/> as an <c>IPrivacyDataProvider</c>
    /// and adds <c>"auditing"</c> to the scatter-gather registry.
    /// </summary>
    /// <remarks>
    /// The matching Wolverine handler is discovered automatically. Requires
    /// <see cref="GranitAuditingWolverineModule"/> to be loaded.
    /// </remarks>
    public static GranitPrivacyBuilder AddGranitAuditingPrivacyProvider(
        this GranitPrivacyBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddDataProvider<AuditingPrivacyDataProvider>();
    }
}
