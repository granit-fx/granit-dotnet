using Granit.Core.MultiTenancy;
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
            .WithTags(options.TagName);

        group.MapGet("", HandleGetAllGlobalSettingsAsync)
             .RequireAuthorization(SettingsPermissions.Global.Read)
             .WithName("GetAllGlobalSettings")
             .WithSummary("Returns all settings at the global scope.")
             .WithDescription("Returns all settings defined at the global (application-wide) scope as a key-value dictionary. Global settings serve as the base layer in the cascading resolution chain (User → Tenant → Global). Requires the Settings.Global.Read permission.")
             .Produces<IReadOnlyDictionary<string, string?>>();

        group.MapPut("/{name}", HandlePutGlobalSettingAsync)
             .RequireAuthorization(SettingsPermissions.Global.Manage)
             .WithName("UpdateGlobalSetting")
             .WithSummary("Sets a global-level setting value.")
             .WithDescription("Sets or clears a global-level setting value. Pass null to remove the override and revert to the definition's default. The setting name must match a registered setting definition (returns 404 otherwise). Requires the Settings.Global.Manage permission.")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status404NotFound);

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
            .WithTags(options.TagName);

        group.MapGet("", HandleGetAllTenantSettingsAsync)
             .RequireAuthorization(SettingsPermissions.Tenant.Read)
             .WithName("GetAllTenantSettings")
             .WithSummary("Returns all settings at the current tenant scope.")
             .WithDescription("Returns all settings defined at the tenant scope for the current tenant. Tenant settings override global values in the cascading resolution chain. Requires the Settings.Tenant.Read permission. Returns 400 if no tenant context is available.")
             .Produces<IReadOnlyDictionary<string, string?>>()
             .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPut("/{name}", HandlePutTenantSettingAsync)
             .RequireAuthorization(SettingsPermissions.Tenant.Manage)
             .WithName("UpdateTenantSetting")
             .WithSummary("Sets a tenant-level setting value.")
             .WithDescription("Sets or clears a tenant-level setting value for the current tenant. Pass null to remove the tenant override and fall back to the global value. The setting name must match a registered setting definition (returns 404 otherwise). Requires the Settings.Tenant.Manage permission.")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status404NotFound);

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
            result[sv.Name] = sv.Value;
        }

        return TypedResults.Ok<IReadOnlyDictionary<string, string?>>(result);
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
            result[sv.Name] = sv.Value;
        }

        return TypedResults.Ok<IReadOnlyDictionary<string, string?>>(result);
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

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static ProblemHttpResult NoTenantContext() =>
        TypedResults.Problem(
            detail: "No tenant context is available. Multi-tenancy may not be configured.",
            statusCode: StatusCodes.Status400BadRequest);
}
