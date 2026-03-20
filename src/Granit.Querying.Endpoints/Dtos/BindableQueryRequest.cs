using System.Reflection;
using Granit.Querying.Endpoints.Binding;
using Microsoft.AspNetCore.Http;

namespace Granit.Querying.Endpoints.Dtos;

/// <summary>
/// ASP.NET Core Minimal API–bindable wrapper around <see cref="QueryRequest"/>.
/// Parses <c>filter[field.op]=value</c> and <c>presets[group]=name</c> from the query string.
/// </summary>
/// <remarks>
/// Implements the static <c>BindAsync</c> convention so the type can be used
/// directly as a parameter in Minimal API endpoint handlers.
/// </remarks>
public sealed class BindableQueryRequest
{
    /// <summary>
    /// The parsed query request.
    /// </summary>
    public QueryRequest Value { get; }

    private BindableQueryRequest(QueryRequest value) => Value = value;

    /// <summary>
    /// Creates a <see cref="BindableQueryRequest"/> from an existing <see cref="QueryRequest"/>.
    /// Intended for unit testing only.
    /// </summary>
    internal static BindableQueryRequest FromQueryRequest(QueryRequest value) => new(value);

    /// <summary>
    /// Binds a <see cref="BindableQueryRequest"/> from the HTTP context query string.
    /// This method is called automatically by ASP.NET Core Minimal API parameter binding.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="parameter">The parameter being bound (unused).</param>
    /// <returns>A <see cref="ValueTask{BindableQueryRequest}"/> containing the parsed request.</returns>
    public static async ValueTask<BindableQueryRequest?> BindAsync(
        HttpContext context, ParameterInfo parameter)
    {
        QueryRequest? request = await QueryRequestBinder.BindAsync(context, parameter)
            .ConfigureAwait(false);

        return request is null ? null : new BindableQueryRequest(request);
    }
}
