namespace Granit.Mentions.Exceptions;

/// <summary>
/// Raised when two registered <see cref="IMentionResolver"/> declare the same
/// <see cref="IMentionResolver.Type"/>. A mention type maps to exactly one resolver.
/// </summary>
public sealed class DuplicateMentionResolverException(string type)
    : InvalidOperationException($"More than one mention resolver is registered for the type '{type}'. Mention types must be unique.")
{
    /// <summary>The duplicated mention type.</summary>
    public string Type { get; } = type;
}
