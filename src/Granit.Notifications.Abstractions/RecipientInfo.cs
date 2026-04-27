namespace Granit.Notifications;

/// <summary>
/// Party information for a notification recipient, resolved by <see cref="Abstractions.IRecipientResolver"/>.
/// </summary>
public sealed record RecipientInfo
{
    /// <summary>User identifier.</summary>
    public required string UserId { get; init; }

    /// <summary>Email address (for Email channel).</summary>
    public string? Email { get; init; }

    /// <summary>Phone number in E.164 format (for SMS/WhatsApp channels).</summary>
    public string? PhoneNumber { get; init; }

    /// <summary>Preferred culture/language (BCP 47, e.g. "fr").</summary>
    public string? PreferredCulture { get; init; }

    /// <summary>Display name for personalization.</summary>
    public string? DisplayName { get; init; }
}
