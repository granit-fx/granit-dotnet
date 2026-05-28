using System.Text.RegularExpressions;

namespace Granit.Presence.Abstractions;

/// <summary>
/// Identifies a "room" — a single resource that multiple users may concurrently
/// view or edit. Combined with a tenant scope, drives the cache-key for
/// <see cref="IResourcePresenceTracker"/>.
/// </summary>
/// <param name="Kind">
/// Resource kind discriminator (e.g. <c>"document"</c>, <c>"cms.page"</c>). MUST match
/// <c>^[a-z][a-z0-9_.-]{0,63}$</c> — lowercase, kebab/snake/dotted, ≤ 64 chars — to bound
/// metric tag cardinality and keep room keys URL-friendly.
/// </param>
/// <param name="Id">
/// Opaque resource identifier (≤ 256 chars). Typically a stringified Guid, slug, or
/// composite key chosen by the consuming module.
/// </param>
public readonly partial record struct ResourceRef(string Kind, string Id)
{
    /// <summary>Maximum length of <see cref="Id"/>.</summary>
    public const int MaxIdLength = 256;

    /// <summary>Maximum length of <see cref="Kind"/>.</summary>
    public const int MaxKindLength = 64;

    /// <summary>Throws when <see cref="Kind"/> / <see cref="Id"/> are missing or out-of-range.</summary>
    /// <exception cref="ArgumentException">When validation fails.</exception>
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrEmpty(Kind);
        ArgumentException.ThrowIfNullOrEmpty(Id);

        if (!KindPattern().IsMatch(Kind))
        {
            throw new ArgumentException(
                $"Resource kind '{Kind}' is invalid. Must match ^[a-z][a-z0-9_.-]{{0,63}}$.",
                nameof(Kind));
        }

        if (Id.Length > MaxIdLength)
        {
            throw new ArgumentException(
                $"Resource id length {Id.Length} exceeds maximum {MaxIdLength}.",
                nameof(Id));
        }
    }

    [GeneratedRegex("^[a-z][a-z0-9_.-]{0,63}$")]
    private static partial Regex KindPattern();
}
