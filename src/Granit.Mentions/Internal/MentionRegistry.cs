using System.Diagnostics.CodeAnalysis;
using Granit.Mentions.Exceptions;

namespace Granit.Mentions.Internal;

/// <summary>
/// Default <see cref="IMentionRegistry"/>. Aggregates every registered <see cref="IMentionResolver"/>,
/// validating type uniqueness on construction so a misconfiguration fails fast at the first scope
/// resolution rather than mid-use.
/// </summary>
internal sealed class MentionRegistry : IMentionRegistry
{
    private readonly Dictionary<string, IMentionResolver> _byType;

    public MentionRegistry(IEnumerable<IMentionResolver> resolvers)
    {
        Resolvers = [.. resolvers];
        _byType = new Dictionary<string, IMentionResolver>(Resolvers.Count, StringComparer.OrdinalIgnoreCase);

        foreach (IMentionResolver resolver in Resolvers)
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

    public IReadOnlyList<IMentionResolver> Resolvers { get; }

    public bool TryGet(string type, [NotNullWhen(true)] out IMentionResolver? resolver) =>
        _byType.TryGetValue(type, out resolver);
}
