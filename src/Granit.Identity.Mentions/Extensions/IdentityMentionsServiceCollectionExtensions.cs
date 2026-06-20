using Granit.Mentions.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Identity.Mentions.Extensions;

/// <summary>Registration extension for the identity-backed <c>@user</c> mention resolver.</summary>
public static class IdentityMentionsServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="UserMentionResolver"/> as a Granit mention resolver, exposing the
    /// <c>@user</c> type. Searches and resolves users via the active
    /// <see cref="Granit.Identity.IIdentityUserReader"/> under the caller's ACLs.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddIdentityMentions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddGranitMentions(builder => builder.Add<UserMentionResolver>());
    }
}
