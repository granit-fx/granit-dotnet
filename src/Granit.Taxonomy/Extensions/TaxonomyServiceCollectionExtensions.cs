using Granit.Diagnostics;
using Granit.Domain;
using Granit.Events;
using Granit.Taxonomy.Authorization;
using Granit.Taxonomy.Diagnostics;
using Granit.Taxonomy.Internal;
using Granit.Taxonomy.Options;
using Granit.Taxonomy.Registration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Granit.Taxonomy.Extensions;

/// <summary>
/// Extensions for registering Granit.Taxonomy module services.
/// </summary>
public static class TaxonomyServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Granit.Taxonomy module.
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <param name="configure">
    /// Optional callback to override the defaults of <see cref="TaxonomyOptions"/>.
    /// When omitted, options are bound from the <c>"Taxonomy"</c> configuration section.
    /// </param>
    /// <remarks>
    /// Registers options binding plus diagnostics
    /// (<see cref="TaxonomyMetrics"/> meter and <c>Granit.Taxonomy</c>
    /// <see cref="System.Diagnostics.ActivitySource"/>). Tag persistence and CRUD
    /// services are wired by <c>Granit.Taxonomy.EntityFrameworkCore</c>
    /// (<c>AddGranitTaxonomyEntityFrameworkCore</c>).
    /// </remarks>
    public static IServiceCollection AddGranitTaxonomy(
        this IServiceCollection services,
        Action<TaxonomyOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        GranitActivitySourceRegistry.Register(TaxonomyActivitySource.Name);
        services.TryAddSingleton<TaxonomyMetrics>();
        services.TryAddSingleton<TaggableTypeRegistry>(static sp =>
        {
            TaggableTypeRegistry registry = new();
            foreach (ITaggableRegistration reg in sp.GetServices<ITaggableRegistration>())
            {
                registry.Register(reg.TargetType, reg.Scope);
            }
            return registry;
        });
        services.TryAddSingleton<ITaggablePermissionResolver, AllowAllTaggablePermissionResolver>();

        OptionsBuilder<TaxonomyOptions> optionsBuilder = services
            .AddOptions<TaxonomyOptions>()
            .BindConfiguration(TaxonomyOptions.SectionName);

        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        return services;
    }

    /// <summary>
    /// Registers <typeparamref name="TAggregate"/> as a taggable target under
    /// <paramref name="scope"/>. The full type name is used as the polymorphic
    /// <c>TargetType</c> discriminator on <c>TagAssignment</c> rows.
    /// </summary>
    /// <typeparam name="TAggregate">Aggregate root type to taggable.</typeparam>
    /// <param name="services">The DI service collection.</param>
    /// <param name="scope">Tag scope under which tags applied to this aggregate are managed (e.g. <c>"documents"</c>).</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddTaggableEntity<TAggregate>(
        this IServiceCollection services,
        string scope)
        where TAggregate : Entity, IEmitEntityLifecycleEvents
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        // The registry is a singleton — register a startup callback that mutates it
        // on first resolution. Eager registration during DI configuration would race
        // with potentially multiple AddTaggableEntity calls in different module
        // ConfigureServices methods.
        services.AddSingleton<ITaggableRegistration>(
            new TaggableRegistration(typeof(TAggregate).FullName ?? typeof(TAggregate).Name, scope));

        // T5.1 — synchronous orphan cleanup. One closed-generic handler per
        // taggable type lets the local event bus dispatch directly without a
        // reflection-based fan-out at the call site.
        services.AddScoped<
            ILocalEventHandler<EntityDeletedEvent<TAggregate>>,
            TaxonomyAssignmentCleanupHandler<TAggregate>>();
        return services;
    }
}

/// <summary>
/// Marker captured by DI for each <c>AddTaggableEntity&lt;T&gt;</c> call. Resolved into
/// the singleton <see cref="TaggableTypeRegistry"/> when the registry is first created.
/// </summary>
internal interface ITaggableRegistration
{
    string TargetType { get; }
    string Scope { get; }
}

internal sealed record TaggableRegistration(string TargetType, string Scope) : ITaggableRegistration;

