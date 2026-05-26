using Granit.Privacy.DataExport.Audit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Privacy.Auditing.Extensions;

/// <summary>
/// DI extensions for <c>Granit.Privacy.Auditing</c>.
/// </summary>
public static class PrivacyAuditingServiceCollectionExtensions
{
    /// <summary>
    /// Replaces the default <c>NullPrivacyExportAuditWriter</c> with
    /// <see cref="AuditingPrivacyExportAuditWriter"/>, which persists every
    /// privacy-export lifecycle event via <c>IAuditingWriter</c>. Opt-in.
    /// </summary>
    public static IServiceCollection AddGranitPrivacyExportAuditing(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Replace(ServiceDescriptor.Scoped<IPrivacyExportAuditWriter, AuditingPrivacyExportAuditWriter>());
        return services;
    }
}
