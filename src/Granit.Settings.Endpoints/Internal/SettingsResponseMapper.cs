using Granit.Settings.Definitions;
using Granit.Settings.Endpoints.Dtos;
using Granit.Settings.Services;
using Granit.Settings.Values;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Settings.Endpoints.Internal;

/// <summary>
/// Shared helpers for building settings responses and problem results.
/// </summary>
internal static class SettingsResponseMapper
{
    /// <summary>Builds the admin payload listing all settings enriched with definition metadata.</summary>
    public static async Task<IReadOnlyList<AdminAppSettingResponse>> BuildAdminPayloadAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        SettingDefinitionRegistry definitionManager =
            context.RequestServices.GetRequiredService<SettingDefinitionRegistry>();
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

    /// <summary>Returns true when the given provider code is allowed for the setting (or the allow-list is empty).</summary>
    public static bool IsProviderAllowed(SettingDefinition definition, string providerName) =>
        definition.Providers.Count == 0 || definition.Providers.Contains(providerName);

    /// <summary>404 problem for an unknown or client-invisible setting.</summary>
    public static ProblemHttpResult SettingNotFound(string name) =>
        TypedResults.Problem(
            detail: $"Setting '{name}' is not declared or not visible to clients.",
            statusCode: StatusCodes.Status404NotFound);

    /// <summary>400 problem when the provider scope is not allowed for a setting.</summary>
    public static ProblemHttpResult ProviderNotAllowed(string name, string scope) =>
        TypedResults.Problem(
            detail: $"Setting '{name}' does not allow the {scope} scope.",
            statusCode: StatusCodes.Status400BadRequest);

    /// <summary>400 problem when a value fails the setting's validation (kind, allow-list, length).</summary>
    public static ProblemHttpResult ValidationFailed(string name) =>
        TypedResults.Problem(
            detail: $"The value for setting '{name}' does not match its expected format, allow-list or length.",
            statusCode: StatusCodes.Status400BadRequest);

    /// <summary>400 problem when no tenant context is available.</summary>
    public static ProblemHttpResult NoTenantContext() =>
        TypedResults.Problem(
            detail: "No tenant context is available. Multi-tenancy may not be configured.",
            statusCode: StatusCodes.Status400BadRequest);
}
