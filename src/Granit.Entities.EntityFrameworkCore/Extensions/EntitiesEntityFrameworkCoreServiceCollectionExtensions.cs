using Granit.Entities.Endpoints;
using Granit.Entities.Endpoints.Internal;
using Granit.Entities.EntityFrameworkCore.Internal;
using Granit.Entities.Layouts;
using Granit.Entities.Relations;
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

    /// <summary>
    /// Registers a closed-generic
    /// <see cref="RelationAggregateCacheInvalidator{TRelated}"/> per related entity
    /// type that participates in at least one relation declaration — both
    /// intra-module declarations on <see cref="EntityDefinitionBuilder{TEntity}"/>
    /// and cross-module grafts via <see cref="IEntityRelationContributor"/>.
    /// Story #1793.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MUST be called AFTER every <c>AddEntityDefinition&lt;T, TDef&gt;()</c> and
    /// <c>AddEntityRelationContribution&lt;T&gt;()</c> registration — the scan
    /// iterates the descriptor and contributor service descriptors collected in
    /// the container at the time of this call.
    /// </para>
    /// <para>
    /// The related entity type must implement
    /// <see cref="Granit.Domain.IEmitEntityLifecycleEvents"/>; otherwise the
    /// invalidator silently skips it (the cached aggregates still expire via
    /// the sliding TTL, just not surgically on each write).
    /// </para>
    /// </remarks>
    public static IServiceCollection AddGranitEntitiesRelationAggregateInvalidation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        List<IEntityDefinitionDescriptor> definitions = MaterializeEntityDefinitions(services);
        var wireNameByClrType = definitions.ToDictionary(d => d.EntityType, d => d.Name);

        // Group every relation declaration by related CLR type → list of
        // (sourceEntityName, relationName) tag inputs.
        Dictionary<Type, List<string>> tagsByRelatedType = [];

        // Pass 1 — intra-module relations carried directly on each descriptor.
        foreach (IEntityDefinitionDescriptor definition in definitions)
        {
            string sourceName = definition.Name;
            foreach (RelationDescriptor relation in definition.Descriptor.Relations
                .Where(r => r.ContributorAssemblyName is null))
            {
                AddTag(tagsByRelatedType, relation.TargetEntityClrType,
                    RelationAggregateCacheKey.EvictionTagForRelation(sourceName, relation.Name));
            }
        }

        // Pass 2 — cross-module contributions resolved through the contribution
        // context (replays the same `Contribute` call the runtime registry uses).
        foreach (IEntityRelationContributor contributor in MaterializeRelationContributors(services))
        {
            EntityRelationContributionContext context = new();
            contributor.Contribute(context);
            foreach ((Type sourceType, IReadOnlyList<RelationDescriptor> contributed) in context.Contributions)
            {
                if (!wireNameByClrType.TryGetValue(sourceType, out string? sourceName))
                {
                    continue; // dropped contributions — same silent-skip semantic as EntityRelationMerger
                }
                foreach (RelationDescriptor relation in contributed)
                {
                    AddTag(tagsByRelatedType, relation.TargetEntityClrType,
                        RelationAggregateCacheKey.EvictionTagForRelation(sourceName, relation.Name));
                }
            }
        }

        // Register one closed-generic invalidator per related type, keyed on the
        // framework's three lifecycle events.
        foreach ((Type relatedType, List<string> evictionTags) in tagsByRelatedType)
        {
            if (!typeof(Granit.Domain.IEmitEntityLifecycleEvents).IsAssignableFrom(relatedType))
            {
                continue;
            }

            Type targetsType = typeof(RelationAggregateInvalidationTargets<>).MakeGenericType(relatedType);
            IReadOnlyList<string> dedupedTags = evictionTags.Distinct(StringComparer.Ordinal).ToArray();
            object targetsInstance = Activator.CreateInstance(targetsType, dedupedTags)!;
            services.AddSingleton(targetsType, targetsInstance);

            Type closedInvalidatorType = typeof(RelationAggregateCacheInvalidator<>).MakeGenericType(relatedType);
            services.AddScoped(
                typeof(ILocalEventHandler<>).MakeGenericType(typeof(EntityCreatedEvent<>).MakeGenericType(relatedType)),
                closedInvalidatorType);
            services.AddScoped(
                typeof(ILocalEventHandler<>).MakeGenericType(typeof(EntityUpdatedEvent<>).MakeGenericType(relatedType)),
                closedInvalidatorType);
            services.AddScoped(
                typeof(ILocalEventHandler<>).MakeGenericType(typeof(EntityDeletedEvent<>).MakeGenericType(relatedType)),
                closedInvalidatorType);
            // Bulk event — same invalidator, opt-in payload for hosts that
            // emit one bulk event instead of N per-row events (story #1794).
            services.AddScoped(
                typeof(ILocalEventHandler<>).MakeGenericType(typeof(EntityBulkUpdatedEvent<>).MakeGenericType(relatedType)),
                closedInvalidatorType);
        }

        return services;
    }

    private static void AddTag(Dictionary<Type, List<string>> bag, Type relatedType, string tag)
    {
        if (!bag.TryGetValue(relatedType, out List<string>? tags))
        {
            tags = [];
            bag[relatedType] = tags;
        }
        tags.Add(tag);
    }

    private static List<IEntityDefinitionDescriptor> MaterializeEntityDefinitions(IServiceCollection services)
    {
        List<IEntityDefinitionDescriptor> descriptors = [];
        foreach (ServiceDescriptor descriptor in services.Where(d => d.ServiceType == typeof(IEntityDefinitionDescriptor)))
        {
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
            descriptors.Add(tempProvider.GetRequiredService<IEntityDefinitionDescriptor>());
        }
        return descriptors;
    }

    private static List<IEntityRelationContributor> MaterializeRelationContributors(IServiceCollection services)
    {
        List<IEntityRelationContributor> contributors = [];
        foreach (ServiceDescriptor descriptor in services.Where(d => d.ServiceType == typeof(IEntityRelationContributor)))
        {
            using ServiceProvider tempProvider = new ServiceCollection()
                .Add(descriptor switch
                {
                    { ImplementationFactory: { } factory } => ServiceDescriptor.Singleton(typeof(IEntityRelationContributor), factory),
                    { ImplementationInstance: { } instance } => ServiceDescriptor.Singleton(typeof(IEntityRelationContributor), instance),
                    { ImplementationType: { } implType } => ServiceDescriptor.Singleton(typeof(IEntityRelationContributor), implType),
                    _ => throw new InvalidOperationException(
                        "IEntityRelationContributor must be registered with a factory, instance or implementation type."),
                })
                .BuildServiceProvider();
            contributors.Add(tempProvider.GetRequiredService<IEntityRelationContributor>());
        }
        return contributors;
    }
}
