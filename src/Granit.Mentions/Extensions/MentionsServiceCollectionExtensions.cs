using Granit.DataLookup.Sources;
using Granit.Mentions.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Mentions.Extensions;

/// <summary>
/// Application-facing registration for the Granit <c>@</c> mention seam. The mention picker is
/// served through <c>Granit.DataLookup</c> via the <c>mentions</c> facade source registered here.
/// </summary>
public static class MentionsServiceCollectionExtensions
{
    /// <summary>
    /// Opens the opt-in builder so the application can expose a curated, ACL-bound set of
    /// <see cref="IMentionResolver"/> the <c>@</c> picker can search and resolve.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Opt-in resolver registrations.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddGranitMentions(
        this IServiceCollection services,
        Action<MentionRegistrationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        AddCoreServices(services);
        configure(new MentionRegistrationBuilder(services));
        return services;
    }

    /// <summary>
    /// Registers the mention registry and the <c>mentions</c> lookup facade without any resolvers.
    /// With none registered, the picker returns no candidates.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddGranitMentions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        AddCoreServices(services);
        return services;
    }

    internal static void AddCoreServices(IServiceCollection services)
    {
        services.TryAddScoped<IMentionRegistry, MentionRegistry>();
        services.TryAddScoped<IMentionAuthorizer, PermissionMentionAuthorizer>();
        // Registered once (idempotent across repeated AddGranitMentions calls) and coexists with
        // any other ILookupSource registrations.
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILookupSource, MentionLookupSource>());
    }
}
