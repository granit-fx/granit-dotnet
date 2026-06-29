using Granit.MultiTenancy;
using Granit.Settings.Definitions;
using Granit.Settings.Endpoints.Dtos;
using Granit.Settings.Endpoints.Internal;
using Granit.Settings.Endpoints.Permissions;
using Granit.Settings.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Settings.Endpoints.Endpoints;

/// <summary>
/// Write Minimal API endpoints for global and tenant setting administration.
/// </summary>
internal static class AdminSettingsWriteEndpoints
{
    /// <summary>Maps all global settings write endpoints to the given route group.</summary>
    public static RouteGroupBuilder MapGlobalSettingsWriteEndpoints(this RouteGroupBuilder group)
    {
        group.MapPut("/{name}", PutGlobalSettingAsync)
             .RequireAuthorization(SettingsPermissions.Global.Manage)
             .WithName("UpdateGlobalSetting")
             .WithSummary("Sets a global-level setting value.")
             .WithDescription("Sets or clears a global-level setting value. Pass null to remove the override and revert to the definition's default. The setting name must match a registered setting definition (returns 404 otherwise). Returns 400 if the Global provider is not allowed for this setting. Requires the Settings.Global.Manage permission.")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/bulk", BulkUpdateGlobalSettingsAsync)
             .RequireAuthorization(SettingsPermissions.Global.Manage)
             .WithName("BulkUpdateGlobalSettings")
             .WithSummary("Applies multiple global setting updates in a single request.")
             .WithDescription("Applies every entry individually — a failure on one entry (unknown key, disallowed provider, invalid value) does not roll back the others. Returns HTTP 200 with a per-entry outcome envelope; clients inspect each BulkSettingResult to determine success. Pass null in Value to clear an override. Malformed request bodies (empty list, too many entries) return 400. Requires the Settings.Global.Manage permission.")
             .Produces<BulkUpdateSettingsResponse>()
             .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return group;
    }

    /// <summary>Maps all tenant settings write endpoints to the given route group.</summary>
    public static RouteGroupBuilder MapTenantSettingsWriteEndpoints(this RouteGroupBuilder group)
    {
        group.MapPut("/{name}", PutTenantSettingAsync)
             .RequireAuthorization(SettingsPermissions.Tenant.Manage)
             .WithName("UpdateTenantSetting")
             .WithSummary("Sets a tenant-level setting value.")
             .WithDescription("Sets or clears a tenant-level setting value for the current tenant. Pass null to remove the tenant override and fall back to the global value. The setting name must match a registered setting definition (returns 404 otherwise). Returns 400 if no tenant context is available or if the Tenant provider is not allowed for this setting. Requires the Settings.Tenant.Manage permission.")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/bulk", BulkUpdateTenantSettingsAsync)
             .RequireAuthorization(SettingsPermissions.Tenant.Manage)
             .WithName("BulkUpdateTenantSettings")
             .WithSummary("Applies multiple tenant setting updates in a single request.")
             .WithDescription("Applies every entry individually for the current tenant — a failure on one entry (unknown key, disallowed provider, invalid value) does not roll back the others. Returns HTTP 200 with a per-entry outcome envelope; clients inspect each BulkSettingResult to determine success. Pass null in Value to clear an override. Returns 400 if no tenant context is available or if the request body is malformed. Requires the Settings.Tenant.Manage permission.")
             .Produces<BulkUpdateSettingsResponse>()
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return group;
    }

    // -------------------------------------------------------------------------
    // Global handlers
    // -------------------------------------------------------------------------

    private static async Task<Results<NoContent, ProblemHttpResult>> PutGlobalSettingAsync(
        HttpContext context,
        string name,
        UpdateSettingValueRequest body,
        CancellationToken cancellationToken)
    {
        SettingDefinitionManager definitionManager =
            context.RequestServices.GetRequiredService<SettingDefinitionManager>();

        SettingDefinition? definition = definitionManager.GetOrNull(name);

        if (definition is null)
        {
            return SettingsResponseMapper.SettingNotFound(name);
        }

        if (!SettingsResponseMapper.IsProviderAllowed(definition, "G"))
        {
            return SettingsResponseMapper.ProviderNotAllowed(name, "Global");
        }

        ISettingManager settingManager =
            context.RequestServices.GetRequiredService<ISettingManager>();

        await settingManager
            .SetGlobalAsync(name, body.Value, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    private static async Task<Ok<BulkUpdateSettingsResponse>> BulkUpdateGlobalSettingsAsync(
        HttpContext context,
        BulkUpdateSettingsRequest body,
        CancellationToken cancellationToken)
    {
        SettingDefinitionManager definitionManager =
            context.RequestServices.GetRequiredService<SettingDefinitionManager>();
        ISettingManager settingManager =
            context.RequestServices.GetRequiredService<ISettingManager>();

        var results = new BulkSettingResult[body.Settings.Count];

        for (int i = 0; i < body.Settings.Count; i++)
        {
            BulkSettingEntry entry = body.Settings[i];
            SettingDefinition? definition = definitionManager.GetOrNull(entry.Key);

            if (definition is null)
            {
                results[i] = new BulkSettingResult(entry.Key, BulkSettingOutcome.NotFound, "Granit:Settings:NotFound");
                continue;
            }

            if (!SettingsResponseMapper.IsProviderAllowed(definition, "G"))
            {
                results[i] = new BulkSettingResult(entry.Key, BulkSettingOutcome.ProviderNotAllowed, "Granit:Settings:ProviderNotAllowed");
                continue;
            }

            if (!definition.IsValidValue(entry.Value))
            {
                results[i] = new BulkSettingResult(entry.Key, BulkSettingOutcome.ValidationFailed, "Granit:Settings:ValidationFailed");
                continue;
            }

            await settingManager
                .SetGlobalAsync(entry.Key, entry.Value, cancellationToken)
                .ConfigureAwait(false);

            results[i] = new BulkSettingResult(entry.Key, BulkSettingOutcome.Updated, null);
        }

        return TypedResults.Ok(new BulkUpdateSettingsResponse(results));
    }

    // -------------------------------------------------------------------------
    // Tenant handlers
    // -------------------------------------------------------------------------

    private static async Task<Results<NoContent, ProblemHttpResult>> PutTenantSettingAsync(
        HttpContext context,
        string name,
        UpdateSettingValueRequest body,
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

        SettingDefinition? definition = definitionManager.GetOrNull(name);

        if (definition is null)
        {
            return SettingsResponseMapper.SettingNotFound(name);
        }

        if (!SettingsResponseMapper.IsProviderAllowed(definition, "T"))
        {
            return SettingsResponseMapper.ProviderNotAllowed(name, "Tenant");
        }

        ISettingManager settingManager =
            context.RequestServices.GetRequiredService<ISettingManager>();

        await settingManager
            .SetForTenantAsync(currentTenant.Id!.Value, name, body.Value, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<BulkUpdateSettingsResponse>, ProblemHttpResult>> BulkUpdateTenantSettingsAsync(
        HttpContext context,
        BulkUpdateSettingsRequest body,
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
        ISettingManager settingManager =
            context.RequestServices.GetRequiredService<ISettingManager>();

        Guid tenantId = currentTenant.Id!.Value;
        var results = new BulkSettingResult[body.Settings.Count];

        for (int i = 0; i < body.Settings.Count; i++)
        {
            BulkSettingEntry entry = body.Settings[i];
            SettingDefinition? definition = definitionManager.GetOrNull(entry.Key);

            if (definition is null)
            {
                results[i] = new BulkSettingResult(entry.Key, BulkSettingOutcome.NotFound, "Granit:Settings:NotFound");
                continue;
            }

            if (!SettingsResponseMapper.IsProviderAllowed(definition, "T"))
            {
                results[i] = new BulkSettingResult(entry.Key, BulkSettingOutcome.ProviderNotAllowed, "Granit:Settings:ProviderNotAllowed");
                continue;
            }

            if (!definition.IsValidValue(entry.Value))
            {
                results[i] = new BulkSettingResult(entry.Key, BulkSettingOutcome.ValidationFailed, "Granit:Settings:ValidationFailed");
                continue;
            }

            await settingManager
                .SetForTenantAsync(tenantId, entry.Key, entry.Value, cancellationToken)
                .ConfigureAwait(false);

            results[i] = new BulkSettingResult(entry.Key, BulkSettingOutcome.Updated, null);
        }

        return TypedResults.Ok(new BulkUpdateSettingsResponse(results));
    }
}
