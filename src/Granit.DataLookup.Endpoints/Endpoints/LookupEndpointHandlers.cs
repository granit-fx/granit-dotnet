using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Granit.Authorization;
using Granit.DataLookup.Descriptors;
using Granit.DataLookup.Diagnostics;
using Granit.DataLookup.Endpoints.Dtos;
using Granit.DataLookup.Registry;
using Granit.DataLookup.Sources;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Granit.DataLookup.Endpoints.Endpoints;

/// <summary>
/// Minimal API handlers for the data-lookup endpoints.
/// </summary>
/// <remarks>
/// Kept as <see langword="public"/> with <see langword="public static"/> method members so that the
/// minimal-API route builder can reference the methods directly and so that the
/// <c>Granit.Analyzers</c> handler-visibility rules are satisfied.
/// </remarks>
public static class LookupEndpointHandlers
{
    /// <summary>GET /lookups — returns the discovery manifest.</summary>
    public static Ok<LookupManifestResponse> GetManifest(
        [FromServices] ILookupRegistry registry)
    {
        IReadOnlyList<LookupManifestEntry> entries = registry.GetManifest();

        LookupManifestEntryResponse[] responses =
        [
            .. entries.Select(e => new LookupManifestEntryResponse(e.Name, e.Kind, e.RequiredPermission, e.ScopeKeys)),
        ];

        return TypedResults.Ok(new LookupManifestResponse(responses));
    }

    /// <summary>GET /lookups/{name} — paginated search.</summary>
    [SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "Minimal-API endpoint — ASP.NET binds [FromServices]/[FromQuery] parameters explicitly; no natural domain wrapper for orthogonal request inputs and DI collaborators.")]
    public static async Task<Results<Ok<LookupResultResponse>, NotFound, ForbidHttpResult, ProblemHttpResult>> SearchAsync(
        string name,
        [FromServices] ILookupRegistry registry,
        [FromServices] IPermissionChecker permissionChecker,
        [FromServices] DataLookupMetrics metrics,
        [FromServices] Granit.MultiTenancy.ICurrentTenant currentTenant,
        HttpContext http,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery(Name = "continuationToken")] string? continuationToken = null,
        CancellationToken cancellationToken = default)
    {
        ILookupSource? source = registry.Resolve(name);
        if (source is null)
        {
            return TypedResults.NotFound();
        }

        if (!await IsAuthorizedAsync(source, permissionChecker, cancellationToken).ConfigureAwait(false))
        {
            return TypedResults.Forbid();
        }

        Dictionary<string, string?> scope = ExtractScope(http.Request.Query);

        string? missingKey = FindMissingScopeKey(source, scope);
        if (missingKey is not null)
        {
            metrics.RecordMissingScope(currentTenant.Id?.ToString(), name, missingKey);
            return TypedResults.Problem(
                detail: $"Missing required scope key '{missingKey}'.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Missing lookup scope");
        }

        LookupQuery query = new(
            Search: search,
            Page: Math.Max(1, page),
            PageSize: Math.Clamp(pageSize, 1, 200),
            Scope: scope,
            ContinuationToken: continuationToken);

        long startTicks = Stopwatch.GetTimestamp();
        LookupResult result = await source.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        double seconds = Stopwatch.GetElapsedTime(startTicks).TotalSeconds;
        metrics.RecordSearch(currentTenant.Id?.ToString(), name, seconds);

        LookupItemResponse[] items = [.. result.Items.Select(i => new LookupItemResponse(i.Value, i.Label, i.Extra))];
        return TypedResults.Ok(new LookupResultResponse(items, result.TotalCount, result.ContinuationToken));
    }

    /// <summary>GET /lookups/{name}/resolve?value=… — single item lookup for rehydration.</summary>
    public static async Task<Results<Ok<LookupItemResponse>, NotFound, ForbidHttpResult, ProblemHttpResult>> ResolveAsync(
        string name,
        [FromQuery] string? value,
        [FromServices] ILookupRegistry registry,
        [FromServices] IPermissionChecker permissionChecker,
        [FromServices] DataLookupMetrics metrics,
        [FromServices] Granit.MultiTenancy.ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return TypedResults.Problem(
                detail: "Missing required 'value' query parameter.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Missing lookup value");
        }

        ILookupSource? source = registry.Resolve(name);
        if (source is null)
        {
            return TypedResults.NotFound();
        }

        if (!await IsAuthorizedAsync(source, permissionChecker, cancellationToken).ConfigureAwait(false))
        {
            return TypedResults.Forbid();
        }

        LookupItem? item = await source.ResolveByValueAsync(value, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return TypedResults.NotFound();
        }

        metrics.RecordResolve(currentTenant.Id?.ToString(), name);
        return TypedResults.Ok(new LookupItemResponse(item.Value, item.Label, item.Extra));
    }

    private static async Task<bool> IsAuthorizedAsync(
        ILookupSource source,
        IPermissionChecker permissionChecker,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source.RequiredPermission))
        {
            return true;
        }

        return await permissionChecker
            .IsGrantedAsync(source.RequiredPermission, cancellationToken)
            .ConfigureAwait(false);
    }

    private static Dictionary<string, string?> ExtractScope(IQueryCollection query)
    {
        Dictionary<string, string?> scope = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> entry in query)
        {
            const string prefix = "scope.";
            if (entry.Key.StartsWith(prefix, StringComparison.Ordinal) && entry.Key.Length > prefix.Length)
            {
                string key = entry.Key[prefix.Length..];
                scope[key] = entry.Value.ToString();
            }
        }

        return scope;
    }

    private static string? FindMissingScopeKey(ILookupSource source, Dictionary<string, string?> scope)
    {
        foreach (string required in source.ScopeKeys)
        {
            if (!scope.TryGetValue(required, out string? value) || string.IsNullOrWhiteSpace(value))
            {
                return required;
            }
        }

        return null;
    }
}
