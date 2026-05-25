using AngleSharp;

namespace Granit.Html.AngleSharp;

/// <summary>
/// Factory for AngleSharp <see cref="IConfiguration"/> instances. Exposes two strictly
/// separated profiles so callers must make the trust decision explicit.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="BuildForUntrustedContent"/> intentionally OMITS the AngleSharp default loader,
/// CSS resolution, and script execution. The architecture test in
/// <c>Granit.Html.AngleSharp.Tests</c> verifies the source code of this type never
/// invokes the default-loader fluent helper.
/// </para>
/// </remarks>
public static class AngleSharpConfiguration
{
    /// <summary>
    /// Configuration suitable for trusted HTML produced by the host (e.g. Granit-owned
    /// email templates rendered through Scriban). Matches the historical Notifications.Email
    /// behaviour — parser only, no CSS engine, no JS engine, no external resource loading.
    /// </summary>
    public static IConfiguration BuildForTrustedTemplates() => Configuration.Default;

    /// <summary>
    /// Configuration suitable for HTML that did not originate from the host
    /// (user uploads, third-party content, indexed documents). NEVER invokes the AngleSharp
    /// default-loader helper — that would open an SSRF channel.
    /// </summary>
    public static IConfiguration BuildForUntrustedContent() => Configuration.Default;
}
