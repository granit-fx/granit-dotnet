using Granit.MultiTenancy;
using Granit.Settings.Definitions;
using Granit.Settings.Endpoints.Dtos;
using Granit.Settings.Endpoints.Options;
using Granit.Settings.Endpoints.Permissions;
using Granit.Settings.Services;
using Granit.Settings.Values;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Settings.Endpoints.Extensions;

/// <summary>
/// Extension methods for mapping global and tenant setting administration endpoints.
/// </summary>
public static class AdminSettingsEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps global setting administration endpoints under <c>/{prefix}/settings/global</c>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="SettingsEndpointsOptions"/>.</param>
    /// <returns>The route group builder for further chaining.</returns>
    public static RouteGroupBuilder MapGranitGlobalSettings(
        this IEndpointRouteBuilder endpoints,
        Action<SettingsEndpointsOptions>? configure = null)
    {
        SettingsEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.GlobalRoutePrefix)
            .WithTags(options.GlobalTagName);

        group.MapGet("", HandleGetAllGlobalSettingsAsync)
             .RequireAuthorization(SettingsPermissions.Global.Read)
             .WithName("GetAllGlobalSettings")
             .WithSummary("Returns all settings at the global scope.")
             .WithDescription("Returns all settings defined at the global (application-wide) scope as a key-value dictionary. Global settings serve as the base layer in the cascading resolution chain (User → Tenant → Global). Requires the Settings.Global.Read permission.")
             .Produces<IReadOnlyDictionary<string, string?>>();

        group.MapGet("/definitions", HandleGetAllGlobalSettingDefinitionsAsync)
             .RequireAuthorization(SettingsPermissions.Global.Read)
             .WithName("GetAllGlobalSettingDefinitions")
             .WithSummary("Returns all global settings enriched with their definition metadata.")
             .WithDescription("Returns every declared setting alongside its metadata (display label, description, default value, value kind, optional allow-list) and the current resolved value at the global scope. Intended for admin UIs that need to render a typed form per setting without guessing value types. The IsVisibleToClients flag is ignored — admins see all settings. Encrypted values are returned masked as '***'. Requires the Settings.Global.Read permission.")
             .Produces<IReadOnlyList<AdminAppSettingResponse>>();

        group.MapPut("/{name}", HandlePutGlobalSettingAsync)
             .RequireAuthorization(SettingsPermissions.Global.Manage)
             .WithName("UpdateGlobalSetting")
             .WithSummary("Sets a global-level setting value.")
             .WithDescription("Sets or clears a global-level setting value. Pass null to remove the override and revert to the definition's default. The setting name must match a registered setting definition (returns 404 otherwise). Returns 400 if the Global provider is not allowed for this setting. Requires the Settings.Global.Manage permission.")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/bulk", HandleBulkUpdateGlobalSettingsAsync)
             .RequireAuthorization(SettingsPermissions.Global.Manage)
             .WithName("BulkUpdateGlobalSettings")
             .WithSummary("Applies multiple global setting updates in a single request.")
             .WithDescription("Applies every entry individually — a failure on one entry (unknown key, disallowed provider, invalid value) does not roll back the others. Returns HTTP 200 with a per-entry outcome envelope; clients inspect each BulkSettingResult to determine success. Pass null in Value to clear an override. Malformed request bodies (empty list, too many entries) return 400. Requires the Settings.Global.Manage permission.")
             .Produces<BulkUpdateSettingsResponse>()
             .ProducesValidationProblem();

        return group;
    }

    /// <summary>
    /// Maps tenant-scoped setting administration endpoints under <c>/{prefix}/settings/tenant</c>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="SettingsEndpointsOptions"/>.</param>
    /// <returns>The route group builder for further chaining.</returns>
    public static RouteGroupBuilder MapGranitTenantSettings(
        this IEndpointRouteBuilder endpoints,
        Action<SettingsEndpointsOptions>? configure = null)
    {
        SettingsEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.TenantRoutePrefix)
            .WithTags(options.TenantTagName);

        group.MapGet("", HandleGetAllTenantSettingsAsync)
             .RequireAuthorization(SettingsPermissions.Tenant.Read)
             .WithName("GetAllTenantSettings")
             .WithSummary("Returns all settings at the current tenant scope.")
             .WithDescription("Returns all settings defined at the tenant scope for the current tenant. Tenant settings override global values in the cascading resolution chain. Requires the Settings.Tenant.Read permission. Returns 400 if no tenant context is available.")
             .Produces<IReadOnlyDictionary<string, string?>>()
             .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/definitions", HandleGetAllTenantSettingDefinitionsAsync)
             .RequireAuthorization(SettingsPermissions.Tenant.Read)
             .WithName("GetAllTenantSettingDefinitions")
             .WithSummary("Returns all tenant settings enriched with their definition metadata.")
             .WithDescription("Returns every declared setting alongside its metadata (display label, description, default value, value kind, optional allow-list) and the current resolved value for the current tenant. Intended for tenant-admin UIs that need to render a typed form per setting. The IsVisibleToClients flag is ignored — tenant admins see all settings. Encrypted values are returned masked as '***'. Returns 400 if no tenant context is available. Requires the Settings.Tenant.Read permission.")
             .Produces<IReadOnlyList<AdminAppSettingResponse>>()
             .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPut("/{name}", HandlePutTenantSettingAsync)
             .RequireAuthorization(SettingsPermissions.Tenant.Manage)
             .WithName("UpdateTenantSetting")
             .WithSummary("Sets a tenant-level setting value.")
             .WithDescription("Sets or clears a tenant-level setting value for the current tenant. Pass null to remove the tenant override and fall back to the global value. The setting name must match a registered setting definition (returns 404 otherwise). Returns 400 if no tenant context is available or if the Tenant provider is not allowed for this setting. Requires the Settings.Tenant.Manage permission.")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/bulk", HandleBulkUpdateTenantSettingsAsync)
             .RequireAuthorization(SettingsPermissions.Tenant.Manage)
             .WithName("BulkUpdateTenantSettings")
             .WithSummary("Applies multiple tenant setting updates in a single request.")
             .WithDescription("Applies every entry individually for the current tenant — a failure on one entry (unknown key, disallowed provider, invalid value) does not roll back the others. Returns HTTP 200 with a per-entry outcome envelope; clients inspect each BulkSettingResult to determine success. Pass null in Value to clear an override. Returns 400 if no tenant context is available or if the request body is malformed. Requires the Settings.Tenant.Manage permission.")
             .Produces<BulkUpdateSettingsResponse>()
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesValidationProblem();

        return group;
    }

    // -------------------------------------------------------------------------
    // Global handlers
    // -------------------------------------------------------------------------

    private static async Task<Ok<IReadOnlyDictionary<string, string?>>> HandleGetAllGlobalSettingsAsync(
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

    private static async Task<Ok<IReadOnlyList<AdminAppSettingResponse>>> HandleGetAllGlobalSettingDefinitionsAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AdminAppSettingResponse> payload = await BuildAdminSettingsPayloadAsync(context, cancellationToken)
            .ConfigureAwait(false);
        return TypedResults.Ok(payload);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> HandlePutGlobalSettingAsync(
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
            return UserSettingsEndpointRouteBuilderExtensions.SettingNotFound(name);
        }

        if (!UserSettingsEndpointRouteBuilderExtensions.IsProviderAllowed(definition, "G"))
        {
            return UserSettingsEndpointRouteBuilderExtensions.ProviderNotAllowed(name, "Global");
        }

        ISettingManager settingManager =
            context.RequestServices.GetRequiredService<ISettingManager>();

        await settingManager
            .SetGlobalAsync(name, body.Value, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    private static async Task<Ok<BulkUpdateSettingsResponse>> HandleBulkUpdateGlobalSettingsAsync(
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

            if (!UserSettingsEndpointRouteBuilderExtensions.IsProviderAllowed(definition, "G"))
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

    private static async Task<Results<Ok<IReadOnlyDictionary<string, string?>>, ProblemHttpResult>> HandleGetAllTenantSettingsAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ICurrentTenant currentTenant =
            context.RequestServices.GetRequiredService<ICurrentTenant>();

        if (!currentTenant.IsAvailable)
        {
            return NoTenantContext();
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

    private static async Task<Results<Ok<IReadOnlyList<AdminAppSettingResponse>>, ProblemHttpResult>> HandleGetAllTenantSettingDefinitionsAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ICurrentTenant currentTenant =
            context.RequestServices.GetRequiredService<ICurrentTenant>();

        if (!currentTenant.IsAvailable)
        {
            return NoTenantContext();
        }

        IReadOnlyList<AdminAppSettingResponse> payload = await BuildAdminSettingsPayloadAsync(context, cancellationToken)
            .ConfigureAwait(false);
        return TypedResults.Ok(payload);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> HandlePutTenantSettingAsync(
        HttpContext context,
        string name,
        UpdateSettingValueRequest body,
        CancellationToken cancellationToken)
    {
        ICurrentTenant currentTenant =
            context.RequestServices.GetRequiredService<ICurrentTenant>();

        if (!currentTenant.IsAvailable)
        {
            return NoTenantContext();
        }

        SettingDefinitionManager definitionManager =
            context.RequestServices.GetRequiredService<SettingDefinitionManager>();

        SettingDefinition? definition = definitionManager.GetOrNull(name);

        if (definition is null)
        {
            return UserSettingsEndpointRouteBuilderExtensions.SettingNotFound(name);
        }

        if (!UserSettingsEndpointRouteBuilderExtensions.IsProviderAllowed(definition, "T"))
        {
            return UserSettingsEndpointRouteBuilderExtensions.ProviderNotAllowed(name, "Tenant");
        }

        ISettingManager settingManager =
            context.RequestServices.GetRequiredService<ISettingManager>();

        await settingManager
            .SetForTenantAsync(currentTenant.Id!.Value, name, body.Value, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<BulkUpdateSettingsResponse>, ProblemHttpResult>> HandleBulkUpdateTenantSettingsAsync(
        HttpContext context,
        BulkUpdateSettingsRequest body,
        CancellationToken cancellationToken)
    {
        ICurrentTenant currentTenant =
            context.RequestServices.GetRequiredService<ICurrentTenant>();

        if (!currentTenant.IsAvailable)
        {
            return NoTenantContext();
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

            if (!UserSettingsEndpointRouteBuilderExtensions.IsProviderAllowed(definition, "T"))
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

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static async Task<IReadOnlyList<AdminAppSettingResponse>> BuildAdminSettingsPayloadAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        SettingDefinitionManager definitionManager =
            context.RequestServices.GetRequiredService<SettingDefinitionManager>();
        ISettingProvider settingProvider =
            context.RequestServices.GetRequiredService<ISettingProvider>();

        IReadOnlyCollection<SettingDefinition> definitions = definitionManager.GetAll();

        if (definitions.Count == 0)
        {
            return [];
        }

        string[] names = definitions.Select(d => d.Name).ToArray();

        IReadOnlyList<SettingValue> values = await settingProvider
            .GetAllAsync(names, cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, string?> valueByName = new(StringComparer.Ordinal);
        foreach (SettingValue sv in values)
        {
            valueByName[sv.Name] = sv.Value;
        }

        List<AdminAppSettingResponse> result = new(definitions.Count);
        foreach (SettingDefinition def in definitions)
        {
            valueByName.TryGetValue(def.Name, out string? value);
            result.Add(new AdminAppSettingResponse(
                Key: def.Name,
                Label: def.DisplayName,
                Description: def.Description,
                DefaultValue: def.DefaultValue,
                Value: def.IsEncrypted ? "***" : value,
                ValueKind: def.ValueKind,
                AllowedValues: def.AllowedValues,
                IsEncrypted: def.IsEncrypted));
        }

        return result;
    }

    private static ProblemHttpResult NoTenantContext() =>
        TypedResults.Problem(
            detail: "No tenant context is available. Multi-tenancy may not be configured.",
            statusCode: StatusCodes.Status400BadRequest);
}
