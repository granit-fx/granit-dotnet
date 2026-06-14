using System.Diagnostics.CodeAnalysis;
using Granit.AI.Chat.Exceptions;
using Granit.AI.Chat.Mentions;

namespace Granit.AI.Chat.Internal;

/// <summary>
/// Default <see cref="IAIMentionRegistry"/>. Aggregates every <see cref="IAIMentionResolver"/>
/// registered via <c>AddGranitChatMentions</c>, validating type uniqueness on construction so a
/// misconfiguration fails fast at the first scope resolution rather than mid-conversation.
/// </summary>
internal sealed class AIMentionRegistry : IAIMentionRegistry
{
    private readonly Dictionary<string, IAIMentionResolver> _byType;

    public AIMentionRegistry(IEnumerable<IAIMentionResolver> resolvers)
    {
        Resolvers = [.. resolvers];
        _byType = new Dictionary<string, IAIMentionResolver>(Resolvers.Count, StringComparer.OrdinalIgnoreCase);

        foreach (IAIMentionResolver resolver in Resolvers)
        {
            if (string.IsNullOrWhiteSpace(resolver.Type))
            {
                throw new InvalidMentionTypeException(resolver.Type ?? string.Empty);
            }

            if (!_byType.TryAdd(resolver.Type, resolver))
            {
                throw new DuplicateMentionResolverException(resolver.Type);
            }
        }
    }

    public IReadOnlyList<IAIMentionResolver> Resolvers { get; }

    public bool TryGet(string type, [NotNullWhen(true)] out IAIMentionResolver? resolver) =>
        _byType.TryGetValue(type, out resolver);
}
