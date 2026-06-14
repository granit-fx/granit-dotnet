using Granit.AI.Chat.Internal;
using Granit.AI.Chat.Suggestions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.AI.Chat.Extensions;

/// <summary>
/// Application-facing registration for the Granit chat suggested-action seam.
/// </summary>
public static class AIChatSuggestionsServiceCollectionExtensions
{
    /// <summary>
    /// Opens the opt-in builder so modules can contribute <see cref="IAISuggestionProvider"/> that
    /// surface typed, non-executing suggested actions alongside the agent's answer.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Opt-in provider registrations.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddGranitChatSuggestions(
        this IServiceCollection services,
        Action<AISuggestionRegistrationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        AddCoreServices(services);
        configure(new AISuggestionRegistrationBuilder(services));
        return services;
    }

    internal static void AddCoreServices(IServiceCollection services) =>
        services.TryAddScoped<IAISuggestionResolver, AISuggestionResolver>();
}
