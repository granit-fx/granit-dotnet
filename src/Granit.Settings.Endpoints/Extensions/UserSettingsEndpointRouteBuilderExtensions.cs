using Granit.Security;
using Granit.Settings.Definitions;
using Granit.Settings.Endpoints.Dtos;
using Granit.Settings.Endpoints.Options;
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
/// Extension methods for mapping user-scoped setting endpoints.
/// </summary>
public static class UserSettingsEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps user-scoped setting endpoints under <c>/{prefix}/settings/user</c>.
    /// All endpoints require authentication.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="SettingsEndpointsOptions"/>.</param>
    /// <returns>The route group builder for further chaining.</returns>
    public static RouteGroupBuilder MapGranitUserSettings(
        this IEndpointRouteBuilder endpoints,
        Action<SettingsEndpointsOptions>? configure = null)
    {
        SettingsEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.UserRoutePrefix)
            .RequireAuthorization()
            .WithTags(options.TagName);

        group.MapGet("", HandleGetAllUserSettingsAsync)
             .WithName("GetAllUserSettings")
             .WithSummary("Returns all client-visible settings resolved for the current user.")
             .WithDescription("Returns all settings marked as client-visible, resolved through the cascading chain (User → Tenant → Global). Only settings with the ClientVisible flag are included. The values reflect the effective configuration for the authenticated user.")
             .Produces<IReadOnlyDictionary<string, string?>>();

        group.MapGet("/{name}", HandleGetUserSettingAsync)
             .WithName("GetUserSetting")
             .WithSummary("Returns a single setting resolved for the current user.")
             .WithDescription("Returns the effective value of a single setting resolved through the cascading chain (User → Tenant → Global). Returns 404 if the setting name does not match any registered setting definition.")
             .Produces<SettingValueResponse>()
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{name}", HandlePutUserSettingAsync)
             .WithName("UpdateUserSetting")
             .WithSummary("Sets a user-level setting value.")
             .WithDescription("Sets a user-level override for the specified setting. This value takes precedence over tenant and global values for this user. Pass null to clear. Returns 404 if the setting name is not defined. Returns 400 if the User provider is not allowed for this setting.")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{name}", HandleDeleteUserSettingAsync)
             .WithName("DeleteUserSetting")
             .WithSummary("Clears the user-level setting value (falls back to tenant/global).")
             .WithDescription("Removes the user-level override for the specified setting. The effective value reverts to the tenant or global value. Returns 404 if the setting name is not defined.")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    // -------------------------------------------------------------------------
    // Handlers
    // -------------------------------------------------------------------------

    private static async Task<Results<Ok<IReadOnlyDictionary<string, string?>>, ProblemHttpResult>> HandleGetAllUserSettingsAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        SettingDefinitionManager definitionManager =
            context.RequestServices.GetRequiredService<SettingDefinitionManager>();
        ISettingProvider settingProvider =
            context.RequestServices.GetRequiredService<ISettingProvider>();

        string[] visibleNames = definitionManager.GetAll()
            .Where(d => d.IsVisibleToClients)
            .Select(d => d.Name)
            .ToArray();

        if (visibleNames.Length == 0)
        {
            return TypedResults.Ok<IReadOnlyDictionary<string, string?>>(
                new Dictionary<string, string?>(StringComparer.Ordinal));
        }

        IReadOnlyList<SettingValue> values = await settingProvider
            .GetAllAsync(visibleNames, cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, string?> result = new(StringComparer.Ordinal);
        foreach (SettingValue sv in values)
        {
            result[sv.Name] = sv.Value;
        }

        return TypedResults.Ok<IReadOnlyDictionary<string, string?>>(result);
    }

    private static async Task<Results<Ok<SettingValueResponse>, ProblemHttpResult>> HandleGetUserSettingAsync(
        HttpContext context,
        string name,
        CancellationToken cancellationToken)
    {
        SettingDefinitionManager definitionManager =
            context.RequestServices.GetRequiredService<SettingDefinitionManager>();

        SettingDefinition? definition = definitionManager.GetOrNull(name);

        if (definition is null || !definition.IsVisibleToClients)
        {
            return SettingNotFound(name);
        }

        ISettingProvider settingProvider =
            context.RequestServices.GetRequiredService<ISettingProvider>();

        string? value = await settingProvider
            .GetOrNullAsync(name, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(new SettingValueResponse(name, value));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> HandlePutUserSettingAsync(
        HttpContext context,
        string name,
        UpdateSettingValueRequest body,
        CancellationToken cancellationToken)
    {
        SettingDefinitionManager definitionManager =
            context.RequestServices.GetRequiredService<SettingDefinitionManager>();

        SettingDefinition? definition = definitionManager.GetOrNull(name);

        if (definition is null || !definition.IsVisibleToClients)
        {
            return SettingNotFound(name);
        }

        if (!IsProviderAllowed(definition, "U"))
        {
            return ProviderNotAllowed(name, "User");
        }

        ICurrentUserService currentUser =
            context.RequestServices.GetRequiredService<ICurrentUserService>();
        ISettingManager settingManager =
            context.RequestServices.GetRequiredService<ISettingManager>();

        await settingManager
            .SetForUserAsync(currentUser.UserId!, name, body.Value, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleDeleteUserSettingAsync(
        HttpContext context,
        string name,
        CancellationToken cancellationToken)
    {
        SettingDefinitionManager definitionManager =
            context.RequestServices.GetRequiredService<SettingDefinitionManager>();

        SettingDefinition? definition = definitionManager.GetOrNull(name);

        if (definition is null || !definition.IsVisibleToClients)
        {
            return SettingNotFound(name);
        }

        ICurrentUserService currentUser =
            context.RequestServices.GetRequiredService<ICurrentUserService>();
        ISettingManager settingManager =
            context.RequestServices.GetRequiredService<ISettingManager>();

        await settingManager
            .DeleteAsync(name, "U", currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    internal static bool IsProviderAllowed(SettingDefinition definition, string providerName) =>
        definition.Providers.Count == 0 || definition.Providers.Contains(providerName);

    internal static ProblemHttpResult SettingNotFound(string name) =>
        TypedResults.Problem(
            detail: $"Setting '{name}' is not declared or not visible to clients.",
            statusCode: StatusCodes.Status404NotFound);

    internal static ProblemHttpResult ProviderNotAllowed(string name, string scope) =>
        TypedResults.Problem(
            detail: $"Setting '{name}' does not allow the {scope} scope.",
            statusCode: StatusCodes.Status400BadRequest);
}
