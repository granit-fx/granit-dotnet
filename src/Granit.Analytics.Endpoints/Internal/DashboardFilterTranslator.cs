using Granit.QueryEngine;

namespace Granit.Analytics.Endpoints.Internal;

/// <summary>
/// Translates the dashboard-level filter dictionary carried by
/// <c>WidgetRenderContext.DashboardFilters</c> into the <c>field.operator</c>
/// keyed dictionary <see cref="QueryRequest.Filter"/> consumes. Dashboard
/// filters use a simple <c>{ field: value }</c> shape; missing operators are
/// defaulted to equality (<c>.eq</c>) so the common case is one keystroke per
/// binding from the frontend.
/// </summary>
/// <remarks>
/// <para>
/// Keys that already encode an operator (i.e. contain a <c>.</c>) pass through
/// unchanged — a caller that wants <c>Amount.gte</c> / <c>Status.in</c> can
/// supply the encoded form directly. The QueryEngine's
/// <c>FilterableField</c> rules apply downstream: unknown fields and
/// non-whitelisted operators are silently dropped, so a malformed filter
/// cannot smuggle in arbitrary SQL.
/// </para>
/// <para>
/// Empty or null input returns <see langword="null"/>, which the QueryEngine
/// treats as "no filter" — same as the inline <c>POST /metrics/{name}</c>
/// endpoint's no-filter call.
/// </para>
/// </remarks>
internal static class DashboardFilterTranslator
{
    /// <summary>
    /// Translates <paramref name="dashboardFilters"/> into a QueryRequest
    /// filter dictionary. Each <c>field → value</c> pair becomes
    /// <c>field.eq → value</c>; pairs whose key already contains <c>.</c>
    /// pass through unchanged.
    /// </summary>
    public static IReadOnlyDictionary<string, string>? ToQueryRequestFilter(
        IReadOnlyDictionary<string, string>? dashboardFilters)
    {
        if (dashboardFilters is null || dashboardFilters.Count == 0)
        {
            return null;
        }

        Dictionary<string, string> result = new(dashboardFilters.Count, StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, string> entry in dashboardFilters)
        {
            string key = entry.Key.Contains('.', StringComparison.Ordinal)
                ? entry.Key
                : $"{entry.Key}.eq";
            result[key] = entry.Value;
        }

        return result;
    }
}
