namespace Granit.Mentions.Exceptions;

/// <summary>
/// Raised when a registered <see cref="IMentionResolver"/> declares a blank
/// <see cref="IMentionResolver.Type"/>. A resolver must declare the mention type it handles.
/// </summary>
public sealed class InvalidMentionTypeException(string type)
    : InvalidOperationException($"A mention resolver declared an invalid type '{type}'. The type must be non-empty.")
{
    /// <summary>The offending type value.</summary>
    public string Type { get; } = type;
}
