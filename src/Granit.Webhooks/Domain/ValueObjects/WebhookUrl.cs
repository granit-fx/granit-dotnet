using Granit.Core.Domain;

namespace Granit.Webhooks.Domain.ValueObjects;

/// <summary>
/// An HTTPS URL for webhook delivery. Maximum 2048 characters.
/// </summary>
public sealed class WebhookUrl : SingleValueObject<string>
{
    private const int MaxLength = 2048;

    /// <inheritdoc />
    public override required string Value { get; init; }

    /// <summary>
    /// Creates a validated <see cref="WebhookUrl"/>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// When <paramref name="value"/> is empty, exceeds 2048 characters, or is not HTTPS.
    /// </exception>
    public static WebhookUrl Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (value.Length > MaxLength)
        {
            throw new ArgumentException(
                $"Webhook URL exceeds maximum length of {MaxLength} characters.", nameof(value));
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            || !string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Webhook URL must be an absolute HTTPS URL.", nameof(value));
        }

        return new WebhookUrl { Value = value };
    }

    /// <summary>Implicit conversion to <see cref="string"/>.</summary>
    public static implicit operator string(WebhookUrl url) => url.Value;

    /// <summary>Implicit conversion from <see cref="string"/>.</summary>
    public static implicit operator WebhookUrl(string value) => Create(value);
}
