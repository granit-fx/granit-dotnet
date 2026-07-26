using Granit.Localization;

namespace Granit.OpenIddict;

/// <summary>
/// Marker class for the <c>OpenIddict</c> localization resource.
/// JSON files: <c>Localization/OpenIddict/{culture}.json</c>, embedded in this assembly.
/// Auto-discovered by <see cref="LocalizationResourceNameAttribute"/>.
/// </summary>
/// <remarks>
/// Backs the <c>LabelKey</c>/<c>DisplayKey</c> values on the OpenIddict application and scope
/// query and entity definitions (e.g. <c>OpenIddict.Columns.ClientId</c>,
/// <c>OpenIddict:Entity.Scope</c>).
/// </remarks>
[LocalizationResourceName("OpenIddict", DefaultCulture = "en")]
public sealed class OpenIddictLocalizationResource;
