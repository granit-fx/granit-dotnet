using Microsoft.Extensions.DependencyInjection;

namespace Granit.AI.Chat.Mentions;

/// <summary>
/// Fluent surface for an application to opt <c>@</c> mention resolvers in. Obtained from
/// <c>AddGranitChatMentions(builder =&gt; ...)</c>. Each <c>Add</c> registers an
/// <see cref="IAIMentionResolver"/> with the container; the <see cref="IAIMentionRegistry"/>
/// then aggregates them for the current scope.
/// </summary>
public sealed class AIMentionRegistrationBuilder(IServiceCollection services)
{
    /// <summary>The underlying service collection, for advanced registration scenarios.</summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// Registers a resolver type, resolved per scope so it can use scoped dependencies
    /// (DbContext, current user, …) and therefore stay bound to the caller's ACLs.
    /// </summary>
    /// <typeparam name="TResolver">The resolver implementation.</typeparam>
    public AIMentionRegistrationBuilder Add<TResolver>()
        where TResolver : class, IAIMentionResolver
    {
        Services.AddScoped<IAIMentionResolver, TResolver>();
        return this;
    }
}
