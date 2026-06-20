using Microsoft.Extensions.DependencyInjection;

namespace Granit.Mentions;

/// <summary>
/// Fluent surface for an application to opt <c>@</c> mention resolvers in. Obtained from
/// <c>AddGranitMentions(builder =&gt; ...)</c>. Each <c>Add</c> registers an
/// <see cref="IMentionResolver"/>; the <see cref="IMentionRegistry"/> aggregates them per scope.
/// </summary>
public sealed class MentionRegistrationBuilder(IServiceCollection services)
{
    /// <summary>The underlying service collection, for advanced registration scenarios.</summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// Registers a resolver type, resolved per scope so it can use scoped dependencies
    /// (DbContext, current user, …) and therefore stay bound to the caller's ACLs.
    /// </summary>
    /// <typeparam name="TResolver">The resolver implementation.</typeparam>
    public MentionRegistrationBuilder Add<TResolver>()
        where TResolver : class, IMentionResolver
    {
        Services.AddScoped<IMentionResolver, TResolver>();
        return this;
    }
}
