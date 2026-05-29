namespace Granit.Domain.ValueObjects;

/// <summary>
/// An absolute HTTP or HTTPS URL. Maximum 2048 characters.
/// </summary>
/// <remarks>
/// Unlike <see cref="HttpsUrl"/>, this permits plain <c>http</c> so a single value
/// object serves development / preview origins (e.g. <c>http://localhost:3000</c>) as
/// well as production. Where HTTPS is mandatory (e.g. a production canonical URL),
/// enforce it in FluentValidation at the endpoint boundary — together with any SSRF /
/// host restrictions — rather than in the value object, so non-production environments
/// are not locked out.
/// </remarks>
public sealed class AbsoluteUrl : SingleValueObject<string>
{
    private const int MaxLength = 2048;

    /// <inheritdoc />
    public override required string Value { get; init; }

    /// <summary>
    /// Creates a validated <see cref="AbsoluteUrl"/>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// When <paramref name="value"/> is empty, exceeds 2048 characters, or is not an
    /// absolute <c>http</c> / <c>https</c> URL.
    /// </exception>
    public static AbsoluteUrl Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (value.Length > MaxLength)
        {
            throw new ArgumentException(
                $"URL exceeds maximum length of {MaxLength} characters.", nameof(value));
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            || (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(
                "Value must be an absolute HTTP or HTTPS URL.", nameof(value));
        }

        return new AbsoluteUrl { Value = value };
    }

    /// <summary>Implicit conversion to <see cref="string"/>.</summary>
    public static implicit operator string(AbsoluteUrl url) => url.Value;

    /// <summary>Implicit conversion from <see cref="string"/>.</summary>
    public static implicit operator AbsoluteUrl(string value) => Create(value);
}
