namespace Granit.Settings.Endpoints.Internal;

/// <summary>
/// Well-known setting names declared by the settings endpoints module.
/// </summary>
/// <remarks>
/// Public so that application modules (e.g. Keycloak sync filters) can reference
/// the canonical setting names without duplicating string constants.
/// </remarks>
public static class WellKnownSettingNames
{
    /// <summary>Preferred locale (BCP 47 language tag, e.g. "fr", "en-GB").</summary>
    public const string PreferredCulture = "Granit.Localization.PreferredCulture";

    /// <summary>Preferred timezone (IANA identifier, e.g. "Europe/Brussels").</summary>
    public const string PreferredTimezone = "Granit.Timing.PreferredTimezone";

    /// <summary>
    /// Preferred first day of the week (a <see cref="DayOfWeek"/> name, e.g. "Monday").
    /// Drives week-relative period tokens (<c>wtd</c>, <c>pw</c>). When unset, the effective
    /// first day is derived from the preferred culture. Grouped with
    /// <see cref="PreferredTimezone"/> under <c>Granit.Timing.*</c>: both feed the period
    /// resolver and both back an ambient provider in <c>Granit.Timing</c> — the culture link
    /// is only the fallback.
    /// </summary>
    public const string PreferredFirstDayOfWeek = "Granit.Timing.PreferredFirstDayOfWeek";
}
