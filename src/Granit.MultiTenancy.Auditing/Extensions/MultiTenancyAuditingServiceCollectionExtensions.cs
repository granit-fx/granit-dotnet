using Granit.MultiTenancy.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.MultiTenancy.Auditing.Extensions;

/// <summary>
/// DI extensions for <c>Granit.MultiTenancy.Auditing</c>.
/// </summary>
public static class MultiTenancyAuditingServiceCollectionExtensions
{
    /// <summary>
    /// Replaces the default no-op <see cref="IHostImpersonationAuditWriter"/> with
    /// <see cref="AuditingHostImpersonationAuditWriter"/>, which persists every
    /// gate decision via <c>IAuditingWriter</c>. Opt-in.
    /// </summary>
    public static IServiceCollection AddGranitHostImpersonationAuditing(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Replace(ServiceDescriptor.Scoped<IHostImpersonationAuditWriter, AuditingHostImpersonationAuditWriter>());
        return services;
    }
}
