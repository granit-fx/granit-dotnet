namespace Granit.Http.Security;

/// <summary>
/// Describes a single URL safety violation: its kind, a human-readable English reason,
/// and the localization key callers can resolve through Granit's
/// <see cref="Microsoft.Extensions.Localization.IStringLocalizer"/> stack.
/// </summary>
/// <param name="Kind">The category of the violation.</param>
/// <param name="Reason">English fallback reason — useful for logs/telemetry.</param>
/// <param name="LocalizationKey">Granit localization key (e.g. <c>UrlSafety:Loopback</c>).</param>
/// <param name="Args">
/// Positional arguments to interpolate into the localized message. Order matches the
/// <c>{0}</c>, <c>{1}</c>, ... placeholders in the resource file (e.g. <c>[host]</c> for
/// most kinds, <c>[host, tld]</c> for <see cref="UrlSafetyViolationKind.ReservedTld"/>,
/// <c>[maxLength]</c> for <see cref="UrlSafetyViolationKind.UrlTooLong"/>).
/// </param>
public sealed record UrlSafetyViolation(
    UrlSafetyViolationKind Kind,
    string Reason,
    string LocalizationKey,
    IReadOnlyList<object?> Args)
{
    /// <summary>Convenience overload for violations without localization arguments.</summary>
    public UrlSafetyViolation(UrlSafetyViolationKind kind, string reason, string localizationKey)
        : this(kind, reason, localizationKey, []) { }
}
