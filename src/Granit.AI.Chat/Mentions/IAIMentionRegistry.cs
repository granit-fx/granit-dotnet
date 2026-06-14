using System.Diagnostics.CodeAnalysis;

namespace Granit.AI.Chat.Mentions;

/// <summary>
/// The set of <see cref="IAIMentionResolver"/> an application has opted in for the current
/// scope, keyed by <see cref="IAIMentionResolver.Type"/>. A type is absent unless a resolver
/// for it was explicitly registered via <c>AddGranitChatMentions</c>.
/// </summary>
public interface IAIMentionRegistry
{
    /// <summary>All registered resolvers, types guaranteed unique.</summary>
    IReadOnlyList<IAIMentionResolver> Resolvers { get; }

    /// <summary>Looks up the resolver for a mention type (case-insensitive).</summary>
    /// <param name="type">The mention type.</param>
    /// <param name="resolver">The resolved resolver, or <see langword="null"/> when none is registered.</param>
    /// <returns><see langword="true"/> when a resolver for that type is registered.</returns>
    bool TryGet(string type, [NotNullWhen(true)] out IAIMentionResolver? resolver);
}
