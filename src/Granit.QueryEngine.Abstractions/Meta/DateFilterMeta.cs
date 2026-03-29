namespace Granit.QueryEngine.Meta;

/// <summary>
/// Date filter metadata for frontend auto-configuration.
/// </summary>
/// <param name="Name">Property name.</param>
/// <param name="DefaultPeriod">The default date period.</param>
/// <param name="AvailablePeriods">All available date periods.</param>
public sealed record DateFilterMeta(
    string Name,
    DatePeriod DefaultPeriod,
    IReadOnlyList<DatePeriod> AvailablePeriods);
