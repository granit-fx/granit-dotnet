using Granit.AI.Chat.Internal;
using Granit.AI.Chat.Mentions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.AI.Chat.Extensions;

/// <summary>
/// Application-facing registration for the Granit chat <c>@</c> mention seam.
/// </summary>
public static class AIChatMentionsServiceCollectionExtensions
{
    /// <summary>
    /// Opens the opt-in builder so the application can expose a curated, ACL-bound set of
    /// <see cref="IAIMentionResolver"/> the chat agent may resolve <c>@</c> references against.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Opt-in resolver registrations.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddGranitChatMentions(
        this IServiceCollection services,
        Action<AIMentionRegistrationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        AddCoreServices(services);
        configure(new AIMentionRegistrationBuilder(services));
        return services;
    }

    /// <summary>
    /// Registers the mention registry and context resolver without any resolvers. With none
    /// registered, mentions on a turn resolve to no context.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddGranitChatMentions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        AddCoreServices(services);
        return services;
    }

    internal static void AddCoreServices(IServiceCollection services)
    {
        services.TryAddScoped<IAIMentionRegistry, AIMentionRegistry>();
        services.TryAddScoped<IAIMentionContextResolver, AIMentionContextResolver>();
        services.TryAddScoped<IAIMentionSearchService, AIMentionSearchService>();
        services.TryAddScoped<IAIMentionAuthorizer, AllowAllMentionAuthorizer>();
    }
}
