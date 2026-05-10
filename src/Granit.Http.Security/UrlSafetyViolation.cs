namespace Granit.Http.Security;

/// <summary>
/// Describes a single URL safety violation: its kind, a human-readable English reason,
/// and the localization key callers can resolve through Granit's
/// <see cref="Microsoft.Extensions.Localization.IStringLocalizer"/> stack.
/// </summary>
/// <param name="Kind">The category of the violation.</param>
/// <param name="Reason">English fallback reason — useful for logs/telemetry.</param>
/// <param name="LocalizationKey">Granit localization key (e.g. <c>UrlSafety:Loopback</c>).</param>
public sealed record UrlSafetyViolation(
    UrlSafetyViolationKind Kind,
    string Reason,
    string LocalizationKey);
