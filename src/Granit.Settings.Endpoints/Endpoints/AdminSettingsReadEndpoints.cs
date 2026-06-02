using Granit.MultiTenancy;
using Granit.Settings.Definitions;
using Granit.Settings.Endpoints.Dtos;
using Granit.Settings.Endpoints.Internal;
using Granit.Settings.Endpoints.Permissions;
using Granit.Settings.Services;
using Granit.Settings.Values;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Settings.Endpoints.Endpoints;

/// <summary>
/// Read-only Minimal API endpoints for global and tenant setting administration.
/// </summary>
internal static class AdminSettingsReadEndpoints
{
    /// <summary>Maps all global settings read endpoints to the given route group.</summary>
    public static RouteGroupBuilder MapGlobalSettingsReadEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("", GetAllGlobalSettingsAsync)
             .RequireAuthorization(SettingsPermissions.Global.Read)
             .WithName("GetAllGlobalSettings")
             .WithSummary("Returns all settings at the global scope.")
             .WithDescription("Returns all settings defined at the global (application-wide) scope as a key-value dictionary. Global settings serve as the base layer in the cascading resolution chain (User → Tenant → Global). Requires the Settings.Global.Read permission.")
             .Produces<IReadOnlyDictionary<string, string?>>();

        group.MapGet("/definitions", GetAllGlobalSettingDefinitionsAsync)
             .RequireAuthorization(SettingsPermissions.Global.Read)
             .WithName("GetAllGlobalSettingDefinitions")
             .WithSummary("Returns all global settings enriched with their definition metadata.")
             .WithDescription("Returns every declared setting alongside its metadata (display label, description, default value, value kind, optional allow-list) and the current resolved value at the global scope. Intended for admin UIs that need to render a typed form per setting without guessing value types. The IsVisibleToClients flag is ignored — admins see all settings. Encrypted values are returned masked as '***'. Requires the Settings.Global.Read permission.")
             .Produces<IReadOnlyList<AdminAppSettingResponse>>();

        return group;
    }

    /// <summary>Maps all tenant settings read endpoints to the given route group.</summary>
    public static RouteGroupBuilder MapTenantSettingsReadEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("", GetAllTenantSettingsAsync)
             .RequireAuthorization(SettingsPermissions.Tenant.Read)
             .WithName("GetAllTenantSettings")
             .WithSummary("Returns all settings at the current tenant scope.")
             .WithDescription("Returns all settings defined at the tenant scope for the current tenant. Tenant settings override global values in the cascading resolution chain. Requires the Settings.Tenant.Read permission. Returns 400 if no tenant context is available.")
             .Produces<IReadOnlyDictionary<string, string?>>()
             .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/definitions", GetAllTenantSettingDefinitionsAsync)
             .RequireAuthorization(SettingsPermissions.Tenant.Read)
             .WithName("GetAllTenantSettingDefinitions")
             .WithSummary("Returns all tenant settings enriched with their definition metadata.")
             .WithDescription("Returns every declared setting alongside its metadata (display label, description, default value, value kind, optional allow-list) and the current resolved value for the current tenant. Intended for tenant-admin UIs that need to render a typed form per setting. The IsVisibleToClients flag is ignored — tenant admins see all settings. Encrypted values are returned masked as '***'. Returns 400 if no tenant context is available. Requires the Settings.Tenant.Read permission.")
             .Produces<IReadOnlyList<AdminAppSettingResponse>>()
             .ProducesProblem(StatusCodes.Status400BadRequest);

        return group;
    }

    // -------------------------------------------------------------------------
    // Global handlers
    // -------------------------------------------------------------------------

    private static async Task<Ok<IReadOnlyDictionary<string, string?>>> GetAllGlobalSettingsAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        SettingDefinitionManager definitionManager =
            context.RequestServices.GetRequiredService<SettingDefinitionManager>();
        ISettingProvider settingProvider =
            context.RequestServices.GetRequiredService<ISettingProvider>();

        string[] allNames = definitionManager.GetAll()
            .Select(d => d.Name)
            .ToArray();

        IReadOnlyList<SettingValue> values = await settingProvider
            .GetAllAsync(allNames, cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, string?> result = new(StringComparer.Ordinal);
        foreach (SettingValue sv in values)
        {
            bool isEncrypted = definitionManager.GetOrNull(sv.Name) is { IsEncrypted: true };
            result[sv.Name] = isEncrypted ? "***" : sv.Value;
        }

        return TypedResults.Ok<IReadOnlyDictionary<string, string?>>(result);
    }

    private static async Task<Ok<IReadOnlyList<AdminAppSettingResponse>>> GetAllGlobalSettingDefinitionsAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AdminAppSettingResponse> payload = await SettingsResponseMapper
            .BuildAdminPayloadAsync(context, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(payload);
    }

    // -------------------------------------------------------------------------
    // Tenant handlers
    // -------------------------------------------------------------------------

    private static async Task<Results<Ok<IReadOnlyDictionary<string, string?>>, ProblemHttpResult>> GetAllTenantSettingsAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ICurrentTenant currentTenant =
            context.RequestServices.GetRequiredService<ICurrentTenant>();

        if (!currentTenant.IsAvailable)
        {
            return SettingsResponseMapper.NoTenantContext();
        }

        SettingDefinitionManager definitionManager =
            context.RequestServices.GetRequiredService<SettingDefinitionManager>();
        ISettingProvider settingProvider =
            context.RequestServices.GetRequiredService<ISettingProvider>();

        string[] allNames = definitionManager.GetAll()
            .Select(d => d.Name)
            .ToArray();

        IReadOnlyList<SettingValue> values = await settingProvider
            .GetAllAsync(allNames, cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, string?> result = new(StringComparer.Ordinal);
        foreach (SettingValue sv in values)
        {
            bool isEncrypted = definitionManager.GetOrNull(sv.Name) is { IsEncrypted: true };
            result[sv.Name] = isEncrypted ? "***" : sv.Value;
        }

        return TypedResults.Ok<IReadOnlyDictionary<string, string?>>(result);
    }

    private static async Task<Results<Ok<IReadOnlyList<AdminAppSettingResponse>>, ProblemHttpResult>> GetAllTenantSettingDefinitionsAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ICurrentTenant currentTenant =
            context.RequestServices.GetRequiredService<ICurrentTenant>();

        if (!currentTenant.IsAvailable)
        {
            return SettingsResponseMapper.NoTenantContext();
        }

        IReadOnlyList<AdminAppSettingResponse> payload = await SettingsResponseMapper
            .BuildAdminPayloadAsync(context, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(payload);
    }
}
