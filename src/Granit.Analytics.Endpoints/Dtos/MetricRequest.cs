namespace Granit.Analytics.Endpoints.Dtos;

/// <summary>
/// Request to evaluate a registered <c>MetricDefinition</c>.
/// </summary>
/// <param name="Period">
/// Time window the metric is evaluated against. Required.
/// </param>
/// <param name="CompareTo">
/// Optional comparison window. When set, the response carries a <c>previous</c>
/// snapshot and a computed delta (<c>deltaRatio</c>, <c>trend</c>, <c>isFavorable</c>).
/// </param>
public sealed record MetricRequest(
    PeriodSpec Period,
    PeriodSpec? CompareTo = null);
