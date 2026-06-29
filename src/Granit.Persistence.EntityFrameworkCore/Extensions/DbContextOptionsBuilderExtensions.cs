using Granit.Persistence.EntityFrameworkCore.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Persistence.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for <see cref="DbContextOptionsBuilder"/> to wire Granit interceptors.
/// </summary>
public static class DbContextOptionsBuilderExtensions
{
    /// <summary>
    /// Adds all registered Granit persistence interceptors to the <see cref="DbContextOptionsBuilder"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Resolves each interceptor from the service provider and adds it if available.
    /// Interceptors are added in the correct order:
    /// <list type="number">
    ///   <item><see cref="AuditedEntityInterceptor"/> — ISO 27001 audit fields (created/modified by/at, tenant, GUID).</item>
    ///   <item><see cref="VersioningInterceptor"/> — auto-assigns <c>VersionId</c> and <c>Version</c> on <c>IVersioned</c> entities.</item>
    ///   <item><see cref="ConcurrencyStampInterceptor"/> — regenerates <c>ConcurrencyStamp</c> on <c>IConcurrencyAware</c> entities.</item>
    ///   <item><see cref="DomainEventDispatcherInterceptor"/> — collects and dispatches domain and integration events from aggregate roots.</item>
    ///   <item><see cref="EntityLifecycleEventInterceptor"/> — auto-dispatches lifecycle events for <c>IEmitEntityLifecycleEvents</c> / <c>IHasEntityEto&lt;TEto&gt;</c> entities.</item>
    ///   <item><see cref="SoftDeleteInterceptor"/> — converts physical deletes to soft deletes for <c>ISoftDeletable</c> entities.</item>
    /// </list>
    /// </para>
    /// <para>
    /// Each interceptor is resolved via <see cref="ServiceProviderServiceExtensions.GetService{T}(IServiceProvider)"/>
    /// and silently skipped if not registered. This allows modules to use <c>UseGranitInterceptors</c>
    /// even when <c>Granit.Persistence.EntityFrameworkCore</c> is not fully configured.
    /// </para>
    /// <para>
    /// Typical usage inside <c>AddDbContextFactory</c>:
    /// <code>
    /// services.AddDbContextFactory&lt;MyDbContext&gt;((sp, options) =&gt;
    /// {
    ///     options.UseNpgsql(connectionString);
    ///     options.UseGranitInterceptors(sp);
    /// }, ServiceLifetime.Scoped);
    /// </code>
    /// </para>
    /// </remarks>
    /// <param name="options">The <see cref="DbContextOptionsBuilder"/> to configure.</param>
    /// <param name="serviceProvider">The service provider to resolve interceptors from.</param>
    /// <returns>The <see cref="DbContextOptionsBuilder"/> for chaining.</returns>
    public static DbContextOptionsBuilder UseGranitInterceptors(
        this DbContextOptionsBuilder options,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        // Granit registers many isolated DbContexts (one per module). Each unique set
        // of options creates an internal EF Core service provider. With 14+ modules
        // the default threshold of 20 is routinely exceeded — suppress the warning
        // since this is by design (modular architecture with isolated persistence).
        options.ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));

        // Bridge the EF Core internal SP to the application SP so interceptors that
        // resolve scoped services at SaveChanges time (e.g. AuditingChangeTrackingInterceptor
        // → ChangeTrackingCaptureService) can reach them via
        // ((IInfrastructure<IServiceProvider>)context).Instance.GetService<T>().
        // EF Core's own AddDbContextFactory wires this automatically; the Granit
        // custom factories (SharedDatabase / TenantPerSchema / TenantPerDatabase)
        // build options manually, so without this call the lookup returns null and
        // audit capture is silently skipped on every isolated DbContext.
        options.UseApplicationServiceProvider(serviceProvider);

        // Make the model cache aware of the registered IGranitModelExtension set (applied at the end of
        // GranitDbContext.OnModelCreating), so a different extension set yields a distinct cached model.
        options.ReplaceService<IModelCacheKeyFactory, GranitModelCacheKeyFactory>();

        // Order matters: Audit → Versioning → ConcurrencyStamp → DomainEvents → SoftDelete.
        // SoftDelete must be last because it converts Deleted → Modified,
        // which would prevent other interceptors from seeing the original state.
        AddInterceptorIfRegistered<AuditedEntityInterceptor>(options, serviceProvider);
        AddInterceptorIfRegistered<VersioningInterceptor>(options, serviceProvider);
        AddInterceptorIfRegistered<ConcurrencyStampInterceptor>(options, serviceProvider);
        AddInterceptorIfRegistered<DomainEventDispatcherInterceptor>(options, serviceProvider);
        AddInterceptorIfRegistered<EntityLifecycleEventInterceptor>(options, serviceProvider);
        AddInterceptorIfRegistered<SoftDeleteInterceptor>(options, serviceProvider);

        // Module-provided interceptors registered via IGranitAutoInterceptor.
        // These run after the standard interceptors so they can observe the final
        // entity state (audit fields set, soft-delete applied, events collected).
        foreach (IGranitAutoInterceptor additional in serviceProvider.GetServices<IGranitAutoInterceptor>())
        {
            options.AddInterceptors(additional);
        }

        return options;
    }

    private static void AddInterceptorIfRegistered<T>(
        DbContextOptionsBuilder options,
        IServiceProvider serviceProvider)
        where T : class, Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor
    {
        T? interceptor = serviceProvider.GetService<T>();
        if (interceptor is not null)
        {
            options.AddInterceptors(interceptor);
        }
    }
}
