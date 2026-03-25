using Granit.Auditing.EntityFrameworkCore.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Auditing.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for adding the audit log interceptor to a DbContext.
/// </summary>
public static class DbContextOptionsBuilderAuditingExtensions
{
    /// <summary>
    /// Adds the <see cref="AuditingChangeTrackingInterceptor"/> to the DbContext options.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Must be called <b>after</b> <c>UseGranitInterceptors(sp)</c> so that audit fields
    /// and soft-delete state are already applied when the audit interceptor reads them.
    /// </para>
    /// <para>
    /// Typical usage:
    /// <code>
    /// services.AddDbContextFactory&lt;AppDbContext&gt;((sp, options) =&gt;
    /// {
    ///     options.UseNpgsql(conn);
    ///     options.UseGranitInterceptors(sp);
    ///     options.UseGranitAuditingInterceptor(sp);
    /// }, ServiceLifetime.Scoped);
    /// </code>
    /// </para>
    /// </remarks>
    /// <param name="options">The DbContext options builder.</param>
    /// <param name="serviceProvider">The service provider to resolve the interceptor from.</param>
    /// <returns>The options builder for chaining.</returns>
    public static DbContextOptionsBuilder UseGranitAuditingInterceptor(
        this DbContextOptionsBuilder options,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        AuditingChangeTrackingInterceptor? interceptor =
            serviceProvider.GetService<AuditingChangeTrackingInterceptor>();

        if (interceptor is not null)
        {
            options.AddInterceptors(interceptor);
        }

        return options;
    }
}
