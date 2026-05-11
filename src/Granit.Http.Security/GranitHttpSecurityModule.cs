using Granit.Http.Security.Extensions;
using Granit.Modularity;

namespace Granit.Http.Security;

/// <summary>
/// Granit module that registers <see cref="IUrlSafetyValidator"/> — the framework's shared
/// SSRF / LFI URL guard.
/// </summary>
/// <remarks>
/// Other Granit modules (<c>Granit.Webhooks</c>, <c>Granit.Browsing</c>, ...) depend on this
/// module rather than reimplementing their own private SSRF allow/deny lists.
/// Conforms to OWASP ASVS V12.6.1, OWASP API7:2023, ISO 27001 A.8.27.
/// </remarks>
public sealed class GranitHttpSecurityModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitUrlSafety();
}
