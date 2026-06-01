using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Workflow.EntityFrameworkCore.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Hostnames.EntityFrameworkCore.Extensions;

/// <summary>
/// Internal helper that registers <c>HostnamesDbContext</c> with the standard Granit
/// interceptors PLUS the <see cref="WorkflowTransitionInterceptor"/> for the hostname
/// workflow audit trail.
/// </summary>
internal static class HostnamesDbContextServiceCollectionExtensions
{
    /// <summary>
    /// Registers the DbContext with <c>UseGranitInterceptors</c> and
    /// the <see cref="WorkflowTransitionInterceptor"/> wired explicitly.
    /// </summary>
    /// <remarks>
    /// Does NOT use the public <c>AddGranitDbContext</c> helper because that helper does not
    /// expose a hook for additional interceptors. This method replicates its host-schema setup
    /// and factory registration, then chains the workflow interceptor.
    /// </remarks>
    internal static IServiceCollection AddGranitDbContextWithWorkflow<TContext>(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configure)
        where TContext : DbContext
    {
        // Replicate GranitDbDefaults.HostDbSchema eager setup from AddGranitDbContext.
        if (GranitDbDefaults.HostDbSchema is null)
        {
            var configuration = services
                .FirstOrDefault(d => d.ServiceType == typeof(IConfiguration))
                ?.ImplementationInstance as IConfiguration;
            string? hostSchema = configuration?["MultiTenancy:TenantIsolation:HostSchema"];
            if (hostSchema is not null)
            {
                GranitDbDefaults.HostDbSchema = hostSchema;
            }
        }

        services.AddDbContextFactory<TContext>((sp, options) =>
        {
            configure(options);
            options.UseGranitInterceptors(sp);

            // Wire WorkflowTransitionInterceptor explicitly. It is NOT a global auto-interceptor
            // (it uses context.Set<WorkflowTransitionRecord>() which would throw on contexts
            // that don't have WorkflowTransitionRecord in their model).
            WorkflowTransitionInterceptor? wti = sp.GetService<WorkflowTransitionInterceptor>();
            if (wti is not null)
            {
                options.AddInterceptors(wti);
            }
        }, ServiceLifetime.Scoped);

        return services;
    }
}
