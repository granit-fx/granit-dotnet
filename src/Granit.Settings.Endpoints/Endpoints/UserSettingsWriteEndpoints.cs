using Granit.Settings.Definitions;
using Granit.Settings.Endpoints.Dtos;
using Granit.Settings.Endpoints.Internal;
using Granit.Settings.Services;
using Granit.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Settings.Endpoints.Endpoints;

/// <summary>
/// Write Minimal API endpoints for user-scoped settings.
/// </summary>
internal static class UserSettingsWriteEndpoints
{
    /// <summary>Maps all user settings write endpoints to the given route group.</summary>
    public static RouteGroupBuilder MapUserSettingsWriteEndpoints(this RouteGroupBuilder group)
    {
        group.MapPut("/{name}", PutUserSettingAsync)
             .WithName("UpdateUserSetting")
             .WithSummary("Sets a user-level setting value.")
             .WithDescription("Sets a user-level override for the specified setting. This value takes precedence over tenant and global values for this user. Pass null to clear. Returns 404 if the setting name is not defined. Returns 400 if the User provider is not allowed for this setting.")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{name}", DeleteUserSettingAsync)
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

    private static async Task<Results<NoContent, ProblemHttpResult>> PutUserSettingAsync(
        HttpContext context,
        string name,
        UpdateSettingValueRequest body,
        CancellationToken cancellationToken)
    {
        SettingDefinitionRegistry definitionManager =
            context.RequestServices.GetRequiredService<SettingDefinitionRegistry>();

        SettingDefinition? definition = definitionManager.GetOrNull(name);

        if (definition is null || !definition.IsVisibleToClients)
        {
            return SettingsResponseMapper.SettingNotFound(name);
        }

        if (!SettingsResponseMapper.IsProviderAllowed(definition, "U"))
        {
            return SettingsResponseMapper.ProviderNotAllowed(name, "User");
        }

        if (!definition.IsValidValue(body.Value))
        {
            return SettingsResponseMapper.ValidationFailed(name);
        }

        ICurrentUserService currentUser =
            context.RequestServices.GetRequiredService<ICurrentUserService>();
        ISettingWriter settingManager =
            context.RequestServices.GetRequiredService<ISettingWriter>();

        await settingManager
            .SetForUserAsync(currentUser.UserId!, name, body.Value, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteUserSettingAsync(
        HttpContext context,
        string name,
        CancellationToken cancellationToken)
    {
        SettingDefinitionRegistry definitionManager =
            context.RequestServices.GetRequiredService<SettingDefinitionRegistry>();

        SettingDefinition? definition = definitionManager.GetOrNull(name);

        if (definition is null || !definition.IsVisibleToClients)
        {
            return SettingsResponseMapper.SettingNotFound(name);
        }

        ICurrentUserService currentUser =
            context.RequestServices.GetRequiredService<ICurrentUserService>();
        ISettingWriter settingManager =
            context.RequestServices.GetRequiredService<ISettingWriter>();

        await settingManager
            .DeleteAsync(name, "U", currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.NoContent();
    }
}
