namespace Granit.Identity.Notifications.Options;

/// <summary>
/// Configuration for <c>IdentityRecipientResolver</c>. Bound to the
/// <c>Identity:RecipientResolver</c> configuration section.
/// </summary>
/// <remarks>
/// <para>
/// The resolver reads contact fields from the canonical <c>User</c> aggregate
/// (ADR-051) when the identity backend returns it — those fields
/// (<c>PhoneNumber</c>, <c>PreferredLocale</c>) are first-class and need no
/// configuration. For backends that surface contact data only through the
/// provider <c>Metadata</c> bag (federated cache entries, custom claims), the
/// resolver falls back to the metadata keys configured here, trying each key in
/// order and taking the first non-empty value.
/// </para>
/// <para>
/// Defaults cover the common provider conventions: Keycloak (<c>phoneNumber</c>,
/// <c>locale</c>), Cognito (<c>phone_number</c>, <c>locale</c>), and Microsoft
/// Graph (<c>preferredLanguage</c>). Providers that store contact data under
/// other keys (e.g. EntraID extension attributes, Firebase custom claims) can
/// extend or replace these lists.
/// </para>
/// </remarks>
public sealed class IdentityRecipientResolverOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Identity:RecipientResolver";

    /// <summary>
    /// Metadata keys probed, in order, to resolve the recipient phone number
    /// when the backend does not expose it as a first-class field. The first
    /// key with a non-empty value wins.
    /// </summary>
    public IList<string> PhoneNumberMetadataKeys { get; set; } = ["phoneNumber", "phone_number"];

    /// <summary>
    /// Metadata keys probed, in order, to resolve the recipient preferred
    /// culture (BCP-47) when the backend does not expose it as a first-class
    /// field. The first key with a non-empty value wins.
    /// </summary>
    public IList<string> PreferredCultureMetadataKeys { get; set; } = ["locale", "preferredLanguage"];

    /// <summary>
    /// Culture (BCP-47) applied when neither the canonical user nor the metadata
    /// bag yields a preferred culture. <see langword="null"/> leaves
    /// <c>RecipientInfo.PreferredCulture</c> unset so the notification pipeline
    /// applies its own default.
    /// </summary>
    public string? DefaultCulture { get; set; }

    /// <summary>
    /// Metadata keys probed, in order, to resolve the recipient preferred time
    /// zone (IANA id) when the backend does not expose it as a first-class field.
    /// The first key with a non-empty value wins. Defaults cover the common
    /// provider conventions (<c>zoneinfo</c> is the OIDC standard claim).
    /// </summary>
    public IList<string> TimeZoneMetadataKeys { get; set; } = ["timezone", "zoneinfo"];
}
