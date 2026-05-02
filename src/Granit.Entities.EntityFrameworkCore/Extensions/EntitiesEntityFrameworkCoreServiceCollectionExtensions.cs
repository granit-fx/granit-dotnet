using Granit.Entities.Endpoints;
using Granit.Entities.EntityFrameworkCore.Internal;
using Granit.Entities.Layouts;
using Granit.Events;
using Granit.QueryEngine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Entities.EntityFrameworkCore.Extensions;

/// <summary>
/// DI extensions that wire the EF Core executor for the Granit.Entities calendar
/// range endpoint.
/// </summary>
public static class EntitiesEntityFrameworkCoreServiceCollectionExtensions
{
    /// <summary>
    /// Replaces the framework's default <c>NullCalendarRangeService</c> with the
    /// EF Core executor and registers one closed-generic
    /// <see cref="ICalendarRangeRunner"/> per <see cref="IEntityDefinitionDescriptor"/>
    /// that declares a <see cref="CalendarLayoutDescriptor"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MUST be called AFTER every <c>AddEntityDefinition&lt;T, TDef&gt;()</c>
    /// registration — the loop iterates the descriptor service descriptors collected
    /// in the container at the time of this call. Adding more entity definitions
    /// after this point silently leaves them unwired (the dispatcher then falls back
    /// to an empty list for the unwired entities, same shape as
    /// <c>NullCalendarRangeService</c>).
    /// </para>
    /// <para>
    /// Lifetime is <c>Scoped</c>: each runner depends on
    /// <see cref="IQueryableSource{TEntity}"/> (DbContext-bound) which captures the
    /// request's <c>ICurrentTenant</c>. A singleton would freeze the first request's
    /// tenant context, producing a cross-tenant data leak on every subsequent call
    /// — the same constraint that <c>Granit.Analytics.EntityFrameworkCore</c>'s
    /// runner registration calls out.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitEntitiesEntityFrameworkCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Replace the no-op default registered by Granit.Entities.Endpoints.
        services.RemoveAll<ICalendarRangeService>();
        services.AddScoped<ICalendarRangeService, EntityFrameworkCoreCalendarRangeService>();

        // Register one closed-generic runner per registered EntityDefinition that
        // exposes a calendar layout. Reflection happens once at composition time;
        // request-time dispatch is a dictionary lookup on entity name.
        IReadOnlyList<ServiceDescriptor> entityDescriptors = [..
            services.Where(d => d.ServiceType == typeof(IEntityDefinitionDescriptor))];

        foreach (ServiceDescriptor descriptor in entityDescriptors)
        {
            // Materialise the descriptor once via a temporary provider — same trick as
            // AddGranitAnalyticsRunners. We need the concrete EntityType + ListLayouts
            // at registration time to skip entities without a calendar layout.
            using ServiceProvider tempProvider = new ServiceCollection()
                .Add(descriptor switch
                {
                    { ImplementationFactory: { } factory } => ServiceDescriptor.Singleton(typeof(IEntityDefinitionDescriptor), factory),
                    { ImplementationInstance: { } instance } => ServiceDescriptor.Singleton(typeof(IEntityDefinitionDescriptor), instance),
                    { ImplementationType: { } implType } => ServiceDescriptor.Singleton(typeof(IEntityDefinitionDescriptor), implType),
                    _ => throw new InvalidOperationException(
                        "IEntityDefinitionDescriptor must be registered with a factory, instance or implementation type."),
                })
                .BuildServiceProvider();

            IEntityDefinitionDescriptor entity = tempProvider.GetRequiredService<IEntityDefinitionDescriptor>();
            bool hasCalendar = entity.Descriptor.ListLayouts.OfType<CalendarLayoutDescriptor>().Any();
            if (!hasCalendar)
            {
                continue;
            }

            Type entityType = entity.EntityType;
            Type runnerInterfaceType = typeof(ICalendarRangeRunner);
            Type closedRunnerType = typeof(CalendarRangeRunner<>).MakeGenericType(entityType);
            Type closedSourceType = typeof(IQueryableSource<>).MakeGenericType(entityType);

            services.AddScoped(runnerInterfaceType, sp =>
            {
                object queryableSource = sp.GetRequiredService(closedSourceType);
                return Activator.CreateInstance(closedRunnerType, queryableSource)!;
            });

            // Cache invalidator — only register when the entity opts into the
            // framework's lifecycle events (IEmitEntityLifecycleEvents). Calendar
            // entities that don't emit lifecycle events still benefit from the
            // sliding TTL but won't get instant invalidation on writes.
            if (typeof(Granit.Domain.IEmitEntityLifecycleEvents).IsAssignableFrom(entityType))
            {
                Type closedInvalidatorType = typeof(CalendarRangeCacheInvalidator<>).MakeGenericType(entityType);
                services.AddScoped(
                    typeof(ILocalEventHandler<>).MakeGenericType(typeof(EntityCreatedEvent<>).MakeGenericType(entityType)),
                    closedInvalidatorType);
                services.AddScoped(
                    typeof(ILocalEventHandler<>).MakeGenericType(typeof(EntityUpdatedEvent<>).MakeGenericType(entityType)),
                    closedInvalidatorType);
                services.AddScoped(
                    typeof(ILocalEventHandler<>).MakeGenericType(typeof(EntityDeletedEvent<>).MakeGenericType(entityType)),
                    closedInvalidatorType);
            }
        }

        return services;
    }
}
