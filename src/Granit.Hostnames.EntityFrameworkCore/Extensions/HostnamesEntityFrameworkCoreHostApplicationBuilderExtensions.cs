using Granit.Hostnames.Contracts;
using Granit.Hostnames.EntityFrameworkCore.Internal;
using Granit.Workflow.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Hostnames.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering EF Core persistence for Granit managed hostnames.
/// </summary>
public static class HostnamesEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers EF Core persistence for Granit managed hostnames.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registers <c>HostnamesDbContext</c>, <c>EfManagedHostnameStore</c> (as both
    /// <see cref="IManagedHostnameReader"/> and <see cref="IManagedHostnameWriter"/>),
    /// <c>EfHostnameResolver</c> as <see cref="IHostnameResolver"/>, and the
    /// <c>WorkflowTransitionInterceptor</c> for the hostname lifecycle audit trail.
    /// </para>
    /// <para>
    /// Must be called after <c>AddGranitHostnames()</c>.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">EF Core options configuration (provider + connection string).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitHostnamesEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        // Register WorkflowTransitionInterceptor + IWorkflowHistoryQuery + IWorkflowTransitionRecorder
        // backed by HostnamesDbContext (which implements IWorkflowDbContext).
        builder.Services.AddGranitWorkflowEntityFrameworkCore<HostnamesDbContext>();

        // Register the DbContext with the standard Granit interceptors AND the
        // WorkflowTransitionInterceptor. Using AddGranitDbContext directly would miss the
        // workflow interceptor (it is NOT a global auto-interceptor to avoid breaking
        // DbContexts that don't have WorkflowTransitionRecord in their model).
        builder.Services.AddGranitDbContextWithWorkflow<HostnamesDbContext>(configure);

        builder.Services.TryAddScoped<EfManagedHostnameStore>();
        builder.Services.TryAddScoped<IManagedHostnameReader>(
            sp => sp.GetRequiredService<EfManagedHostnameStore>());
        builder.Services.TryAddScoped<IManagedHostnameWriter>(
            sp => sp.GetRequiredService<EfManagedHostnameStore>());

        builder.Services.TryAddScoped<IHostnameResolver, EfHostnameResolver>();

        return builder;
    }
}
