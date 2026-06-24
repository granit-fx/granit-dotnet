using Granit.DataLookup.Sources;
using Granit.Mentions.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Mentions.Extensions;

/// <summary>
/// Registration for the <c>@</c> mention picker. A mention is just an existing
/// <c>Granit.DataLookup</c> source opted into the picker via <see cref="AddMentionSource"/>; the
/// <c>mentions</c> facade source fans the picker out across the tagged sources.
/// </summary>
public static class MentionsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <c>mentions</c> facade source. Tag the lookup sources to expose with
    /// <see cref="AddMentionSource"/>. Idempotent.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddGranitMentions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        // Registered once and coexists with any other ILookupSource registrations.
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILookupSource, MentionLookupSource>());
        return services;
    }

    /// <summary>
    /// Tags an existing lookup source (by name) as mentionable, so the <c>@</c> picker includes it.
    /// The source must be registered separately (e.g. <c>AddQueryDefinitionLookup</c>,
    /// <c>AddQueryableLookup</c>). Also ensures the facade is registered. Calling this twice with the
    /// same name registers a duplicate tag; duplicates are collapsed case-insensitively at query time.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="lookupSourceName">The lookup source name to expose, e.g. <c>user</c>, <c>invoice</c>.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddMentionSource(this IServiceCollection services, string lookupSourceName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(lookupSourceName);

        AddGranitMentions(services);
        services.AddSingleton(new MentionSource(lookupSourceName));
        return services;
    }
}
