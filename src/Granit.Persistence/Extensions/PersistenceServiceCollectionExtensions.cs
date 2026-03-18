using Granit.Core.DataFiltering;
using Granit.Core.Events;
using Granit.Http.ExceptionHandling;
using Granit.Persistence.DataSeeding;
using Granit.Persistence.Events;
using Granit.Persistence.ExceptionHandling;
using Granit.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Granit.Persistence.Extensions;

/// <summary>
/// Extensions for registering Granit persistence services.
/// </summary>
public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Adds Granit EF Core services:
    /// <list type="bullet">
    ///   <item>ISO 27001 audit interceptor (<see cref="AuditedEntityInterceptor"/>)</item>
    ///   <item>Versioning interceptor (<see cref="VersioningInterceptor"/>)</item>
    ///   <item>Optimistic concurrency interceptor (<see cref="ConcurrencyStampInterceptor"/>)</item>
    ///   <item>GDPR soft delete interceptor (<see cref="SoftDeleteInterceptor"/>)</item>
    ///   <item>
    ///     Data filter service (<see cref="IDataFilter"/>) for runtime filter control.
    ///     Registered as Singleton: state lives in a <c>static AsyncLocal</c> field,
    ///     not in instance fields.
    ///   </item>
    ///   <item>Domain event dispatcher interceptor (<see cref="DomainEventDispatcherInterceptor"/>)</item>
    ///   <item>Entity lifecycle event interceptor (<see cref="EntityLifecycleEventInterceptor"/>)</item>
    ///   <item>
    ///     No-op <see cref="IDomainEventDispatcher"/> (replaced by Wolverine implementation
    ///     when <c>Granit.Wolverine</c> is configured).
    ///   </item>
    ///   <item>
    ///     No-op <see cref="IIntegrationEventDispatcher"/> (replaced by Wolverine implementation
    ///     when <c>Granit.EventBus.Wolverine</c> is configured).
    ///   </item>
    ///   <item>
    ///     <see cref="EfCoreExceptionStatusCodeMapper"/> if
    ///     <c>Granit.Http.ExceptionHandling</c> is present in the container
    ///     (<see cref="IExceptionStatusCodeMapper"/> already registered).
    ///   </item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddGranitPersistence(this IServiceCollection services)
    {
        services.AddScoped<AuditedEntityInterceptor>();
        services.AddScoped<VersioningInterceptor>();
        services.AddScoped<ConcurrencyStampInterceptor>();
        services.AddScoped<SoftDeleteInterceptor>();
        services.AddScoped<DomainEventDispatcherInterceptor>();
        services.AddScoped<EntityLifecycleEventInterceptor>();
        services.TryAddSingleton<IDomainEventDispatcher, NullDomainEventDispatcher>();
        services.TryAddSingleton<IIntegrationEventDispatcher, NullIntegrationEventDispatcher>();
        services.AddSingleton<IDataFilter, DataFilter>();

        // Register the EF Core exception mapper only when Granit.Http.ExceptionHandling
        // has been configured (IExceptionStatusCodeMapper already in the container).
        // This avoids a hard dependency on ExceptionHandling for consumers that don't use it.
        if (services.Any(d => d.ServiceType == typeof(IExceptionStatusCodeMapper)))
        {
            services.AddSingleton<IExceptionStatusCodeMapper, EfCoreExceptionStatusCodeMapper>();
        }

        return services;
    }

    /// <summary>
    /// Adds the Granit data seeding infrastructure:
    /// <list type="bullet">
    ///   <item><see cref="IDataSeeder"/> as a singleton (orchestrates contributors).</item>
    ///   <item><see cref="DataSeedingHostedService"/> — hosted service that triggers seeding at startup.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// <see cref="IDataSeedContributor"/> implementations must be registered separately
    /// by each module, typically as transient:
    /// <code>
    /// services.AddTransient&lt;IDataSeedContributor, MyModuleSeedContributor&gt;();
    /// </code>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitDataSeeding(this IServiceCollection services)
    {
        services.AddSingleton<IDataSeeder, DataSeeder>();
        services.AddHostedService<DataSeedingHostedService>();
        return services;
    }

    /// <summary>
    /// Adds an EF Core connectivity health check for <typeparamref name="TContext"/>,
    /// tagged <c>"readiness"</c> and <c>"startup"</c>. Uses <c>CanConnectAsync()</c> to verify the database connection.
    /// </summary>
    /// <typeparam name="TContext">The <see cref="DbContext"/> to probe.</typeparam>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">Check name. Defaults to the DbContext type name.</param>
    /// <param name="failureStatus">Status on failure. Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
    public static IHealthChecksBuilder AddGranitDbContextHealthCheck<TContext>(
        this IHealthChecksBuilder builder,
        string? name = null,
        HealthStatus? failureStatus = null)
        where TContext : DbContext
        => builder.AddDbContextCheck<TContext>(
            name: name ?? typeof(TContext).Name,
            failureStatus: failureStatus,
            tags: ["readiness", "startup"]);
}
