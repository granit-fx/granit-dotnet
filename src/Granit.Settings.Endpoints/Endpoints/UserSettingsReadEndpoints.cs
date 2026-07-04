using Granit.Settings.Definitions;
using Granit.Settings.Endpoints.Dtos;
using Granit.Settings.Endpoints.Internal;
using Granit.Settings.Services;
using Granit.Settings.Values;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Settings.Endpoints.Endpoints;

/// <summary>
/// Read-only Minimal API endpoints for user-scoped settings.
/// </summary>
internal static class UserSettingsReadEndpoints
{
    /// <summary>Maps all user settings read endpoints to the given route group.</summary>
    public static RouteGroupBuilder MapUserSettingsReadEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("", GetAllUserSettingsAsync)
             .WithName("GetAllUserSettings")
             .WithSummary("Returns all client-visible settings resolved for the current user.")
             .WithDescription("Returns all settings marked as client-visible, resolved through the cascading chain (User → Tenant → Global). Only settings with the ClientVisible flag are included. The values reflect the effective configuration for the authenticated user.")
             .Produces<IReadOnlyDictionary<string, string?>>();

        group.MapGet("/{name}", GetUserSettingAsync)
             .WithName("GetUserSetting")
             .WithSummary("Returns a single setting resolved for the current user.")
             .WithDescription("Returns the effective value of a single setting resolved through the cascading chain (User → Tenant → Global). Returns 404 if the setting name does not match any registered setting definition.")
             .Produces<SettingValueResponse>()
             .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    // -------------------------------------------------------------------------
    // Handlers
    // -------------------------------------------------------------------------

    private static async Task<Results<Ok<IReadOnlyDictionary<string, string?>>, ProblemHttpResult>> GetAllUserSettingsAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        SettingDefinitionRegistry definitionManager =
            context.RequestServices.GetRequiredService<SettingDefinitionRegistry>();
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
            bool isEncrypted = definitionManager.GetOrNull(sv.Name) is { IsEncrypted: true };
            result[sv.Name] = isEncrypted ? "***" : sv.Value;
        }

        return TypedResults.Ok<IReadOnlyDictionary<string, string?>>(result);
    }

    private static async Task<Results<Ok<SettingValueResponse>, ProblemHttpResult>> GetUserSettingAsync(
        HttpContext context,
        string name,
        CancellationToken cancellationToken)
    {
        SettingDefinitionRegistry definitionManager =
            context.RequestServices.GetRequiredService<SettingDefinitionRegistry>();

        SettingDefinition? definition = definitionManager.GetOrNull(name);

        if (definition?.IsVisibleToClients != true)
        {
            return SettingsResponseMapper.SettingNotFound(name);
        }

        ISettingProvider settingProvider =
            context.RequestServices.GetRequiredService<ISettingProvider>();

        string? value = await settingProvider
            .GetOrNullAsync(name, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(new SettingValueResponse(
            name, definition.IsEncrypted ? "***" : value));
    }
}
