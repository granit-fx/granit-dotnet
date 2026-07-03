namespace Granit.Timing;

/// <summary>
/// Provides the current user's preferred first day of the week (per-request via AsyncLocal).
/// </summary>
/// <remarks>
/// Symmetric to <see cref="ICurrentTimezoneProvider"/>. A <c>null</c> value means
/// "no explicit preference" — consumers derive the effective first day from
/// <c>CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek</c> instead.
/// </remarks>
public interface ICurrentFirstDayOfWeekProvider
{
    /// <summary>
    /// The current user's preferred first day of the week, or <c>null</c> to derive it
    /// from the current culture.
    /// </summary>
    DayOfWeek? FirstDayOfWeek { get; set; }
}
