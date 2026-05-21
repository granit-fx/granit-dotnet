namespace Granit.Localization.Endpoints.Options;

/// <summary>
/// Configuration options for the Granit localization HTTP endpoints.
/// Pass an action to
/// <see cref="Extensions.LocalizationEndpointRouteBuilderExtensions.MapGranitLocalization"/> or
/// <see cref="Extensions.LocalizationEndpointRouteBuilderExtensions.MapGranitLocalizationOverrides"/>
/// to customize the route prefix.
/// </summary>
public sealed class LocalizationEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Localization:Endpoints";

    /// <summary>
    /// Route prefix for both the bootstrapping endpoint and the overrides management endpoints.
    /// Default: <c>"localization"</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>MapGranitLocalization</c> registers <c>GET /{EffectivePrefix}</c>.
    /// <c>MapGranitLocalizationOverrides</c> registers CRUD under <c>/{EffectivePrefix}/overrides</c>.
    /// </para>
    /// </remarks>
    public string RoutePrefix { get; set; } = "localization";

    /// <summary>
    /// OpenAPI tag name for all localization endpoints.
    /// Default: <c>"Localization"</c>.
    /// </summary>
    public string TagName { get; set; } = "Localization";
}
