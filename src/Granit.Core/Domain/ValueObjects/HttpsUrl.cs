namespace Granit.Core.Domain.ValueObjects;

/// <summary>
/// An absolute HTTPS URL. Maximum 2048 characters.
/// </summary>
/// <remarks>
/// Reusable value object for any domain property that must be an HTTPS endpoint:
/// webhook target URLs, callback URLs, redirect URIs, etc.
/// SSRF protection (private IP blocking, TLD restrictions) is NOT handled here —
/// that belongs in FluentValidation at the endpoint boundary.
/// </remarks>
public sealed class HttpsUrl : SingleValueObject<string>
{
    private const int MaxLength = 2048;

    /// <inheritdoc />
    public override required string Value { get; init; }

    /// <summary>
    /// Creates a validated <see cref="HttpsUrl"/>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// When <paramref name="value"/> is empty, exceeds 2048 characters, or is not an absolute HTTPS URL.
    /// </exception>
    public static HttpsUrl Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (value.Length > MaxLength)
        {
            throw new ArgumentException(
                $"URL exceeds maximum length of {MaxLength} characters.", nameof(value));
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            || !string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Value must be an absolute HTTPS URL.", nameof(value));
        }

        return new HttpsUrl { Value = value };
    }

    /// <summary>Implicit conversion to <see cref="string"/>.</summary>
    public static implicit operator string(HttpsUrl url) => url.Value;

    /// <summary>Implicit conversion from <see cref="string"/>.</summary>
    public static implicit operator HttpsUrl(string value) => Create(value);
}
