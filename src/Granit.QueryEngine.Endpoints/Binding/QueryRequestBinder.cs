using System.Reflection;
using Microsoft.AspNetCore.Http;

namespace Granit.QueryEngine.Endpoints.Binding;

/// <summary>
/// Parses <see cref="QueryRequest"/> from an HTTP query string.
/// Handles the <c>filter[field.op]=value</c> and <c>presets[group]=name</c> syntax.
/// </summary>
/// <remarks>
/// <para>Implements the Minimal API <c>BindAsync</c> convention for automatic parameter binding.</para>
/// <para>
/// Query string examples:
/// <list type="bullet">
///   <item><c>?page=1&amp;pageSize=20&amp;sort=-createdAt,lastName</c></item>
///   <item><c>?search=Alice&amp;filter[name.contains]=John&amp;filter[age.gt]=18</c></item>
///   <item><c>?presets[status]=active,pending&amp;groupBy=category</c></item>
/// </list>
/// </para>
/// </remarks>
public static class QueryRequestBinder
{
    /// <summary>
    /// Binds a <see cref="QueryRequest"/> from the HTTP context query string.
    /// This method follows the ASP.NET Core Minimal API <c>BindAsync</c> convention.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="parameter">The parameter being bound (unused).</param>
    /// <returns>A <see cref="ValueTask{QueryRequest}"/> containing the parsed request.</returns>
    public static ValueTask<QueryRequest?> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        IQueryCollection query = context.Request.Query;

        int? page = TryParseInt(query["page"]);
        int? pageSize = TryParseInt(query["pageSize"]);
        string? cursor = query["cursor"].FirstOrDefault();
        string? search = query["search"].FirstOrDefault();
        string? sort = query["sort"].FirstOrDefault();
        string? groupBy = query["groupBy"].FirstOrDefault();
        bool skipTotalCount = string.Equals(query["skipTotalCount"].FirstOrDefault(), "true", StringComparison.OrdinalIgnoreCase);

        Dictionary<string, string>? filter = ParseBracketedParams(query, "filter");
        Dictionary<string, string>? presets = ParseBracketedParams(query, "presets");
        List<string>? quickFilters = ParseCommaSeparatedList(query["quickFilters"]);

        QueryRequest request = new()
        {
            Page = page,
            PageSize = pageSize,
            Cursor = cursor,
            Search = search,
            Sort = sort,
            Filter = filter,
            Presets = presets,
            QuickFilters = quickFilters,
            GroupBy = groupBy,
            SkipTotalCount = skipTotalCount,
        };

        return ValueTask.FromResult<QueryRequest?>(request);
    }

    private static int? TryParseInt(string? value) =>
        int.TryParse(value, out int result) ? result : null;

    /// <summary>
    /// Parses a comma-separated query string value into a list of strings.
    /// For example, <c>quickFilters=MyAppointments,Unread</c> produces <c>["MyAppointments", "Unread"]</c>.
    /// </summary>
    private static List<string>? ParseCommaSeparatedList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string[] parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 0 ? [.. parts] : null;
    }

    /// <summary>
    /// Parses query string keys matching <c>prefix[key]=value</c> into a dictionary.
    /// For example, <c>filter[name.contains]=Alice</c> produces <c>{ "name.contains": "Alice" }</c>.
    /// </summary>
    private static Dictionary<string, string>? ParseBracketedParams(
        IQueryCollection query, string prefix)
    {
        Dictionary<string, string>? result = null;
        string bracketPrefix = prefix + "[";

        foreach (string key in query.Keys)
        {
            if (!key.StartsWith(bracketPrefix, StringComparison.OrdinalIgnoreCase)
                || !key.EndsWith(']'))
            {
                continue;
            }

            string innerKey = key[bracketPrefix.Length..^1];
            if (string.IsNullOrEmpty(innerKey))
            {
                continue;
            }

            string? value = query[key].FirstOrDefault();
            if (value is null)
            {
                continue;
            }

            result ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            result[innerKey] = value;
        }

        return result;
    }
}
