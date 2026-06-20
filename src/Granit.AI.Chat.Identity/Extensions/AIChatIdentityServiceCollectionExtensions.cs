using Granit.AI.Chat.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.AI.Chat.Identity.Extensions;

/// <summary>Registration extension for the identity-backed <c>@user</c> mention resolver.</summary>
public static class AIChatIdentityServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="UserMentionResolver"/> as a Granit AI Chat mention resolver, exposing
    /// the <c>@user</c> type. The resolver searches and resolves users via the active
    /// <see cref="Granit.Identity.IIdentityUserReader"/> under the caller's ACLs.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddIdentityChatMentions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddGranitChatMentions(builder => builder.Add<UserMentionResolver>());
    }
}
