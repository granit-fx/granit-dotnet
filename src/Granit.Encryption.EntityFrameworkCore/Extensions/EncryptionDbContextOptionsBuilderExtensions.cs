using Granit.Encryption.EntityFrameworkCore.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Encryption.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for <see cref="DbContextOptionsBuilder"/> to wire
/// Granit encryption interceptors for per-entity key isolation.
/// </summary>
/// <remarks>
/// <para>
/// Must be called AFTER <c>UseGranitInterceptors(sp)</c> to ensure correct
/// interceptor ordering: <c>AuditedEntityInterceptor</c> assigns entity IDs
/// before <see cref="EncryptionIsolationSaveChangesInterceptor"/> encrypts.
/// </para>
/// <para>
/// Usage:
/// <code>
/// services.AddDbContextFactory&lt;MyDbContext&gt;((sp, options) =&gt;
/// {
///     options.UseNpgsql(connectionString);
///     options.UseGranitInterceptors(sp);
///     options.UseGranitEncryptionInterceptors(sp);
/// }, ServiceLifetime.Scoped);
/// </code>
/// </para>
/// </remarks>
public static class EncryptionDbContextOptionsBuilderExtensions
{
    /// <summary>
    /// Adds encryption isolation interceptors if <see cref="IEntityEncryptionKeyStore"/>
    /// is registered. Silently skips if not registered (encryption isolation is opt-in).
    /// </summary>
    public static DbContextOptionsBuilder UseGranitEncryptionInterceptors(
        this DbContextOptionsBuilder options,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        EncryptionIsolationSaveChangesInterceptor? saveInterceptor =
            serviceProvider.GetService<EncryptionIsolationSaveChangesInterceptor>();
        if (saveInterceptor is not null)
        {
            options.AddInterceptors(saveInterceptor);
        }

        EncryptionIsolationMaterializationInterceptor? materializationInterceptor =
            serviceProvider.GetService<EncryptionIsolationMaterializationInterceptor>();
        if (materializationInterceptor is not null)
        {
            options.AddInterceptors(materializationInterceptor);
        }

        return options;
    }
}
