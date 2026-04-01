namespace Granit.Templating.Scriban.GlobalContexts;

/// <summary>
/// Configuration options for the <c>app</c> template global context.
/// Bind from <c>Granit:Templating:App</c> in appsettings.json.
/// </summary>
/// <remarks>
/// These values are exposed in every Scriban template under <c>{{ app.* }}</c>.
/// All properties are optional — missing values render as empty strings.
/// </remarks>
public sealed class AppGlobalContextOptions
{
    /// <summary>Configuration section path.</summary>
    public const string SectionName = "Granit:Templating:App";

    /// <summary>
    /// The application display name (e.g., <c>"Guava Admin"</c>).
    /// Accessible as <c>{{ app.name }}</c>.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The public base URL of the application (e.g., <c>"https://app.example.com"</c>).
    /// No trailing slash. Accessible as <c>{{ app.base_url }}</c>.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// The support email address (e.g., <c>"support@example.com"</c>).
    /// Accessible as <c>{{ app.support_email }}</c>.
    /// </summary>
    public string SupportEmail { get; set; } = string.Empty;

    /// <summary>
    /// URL to the application logo (e.g., for email headers).
    /// Accessible as <c>{{ app.logo_url }}</c>.
    /// </summary>
    public string LogoUrl { get; set; } = string.Empty;
}
