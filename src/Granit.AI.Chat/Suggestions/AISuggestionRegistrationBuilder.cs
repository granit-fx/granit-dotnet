using Microsoft.Extensions.DependencyInjection;

namespace Granit.AI.Chat.Suggestions;

/// <summary>
/// Fluent surface for a module to contribute <see cref="IAISuggestionProvider"/> implementations.
/// Obtained from <c>AddGranitChatSuggestions(builder =&gt; ...)</c>.
/// </summary>
public sealed class AISuggestionRegistrationBuilder(IServiceCollection services)
{
    /// <summary>The underlying service collection, for advanced registration scenarios.</summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// Registers a suggestion provider, resolved per scope so it can use scoped dependencies
    /// (DbContext, current user, …) and therefore stay bound to the caller's ACLs.
    /// </summary>
    /// <typeparam name="TProvider">The provider implementation.</typeparam>
    public AISuggestionRegistrationBuilder Add<TProvider>()
        where TProvider : class, IAISuggestionProvider
    {
        Services.AddScoped<IAISuggestionProvider, TProvider>();
        return this;
    }
}
