using Granit.Http.Security.Extensions;
using Granit.Modularity;

namespace Granit.Http.Security;

/// <summary>
/// Granit module for HTTP security hardening.
/// </summary>
/// <remarks>
/// <para>
/// Provides OWASP Secure Headers compliant response header configuration:
/// </para>
/// <list type="bullet">
///   <item>Suppresses the Kestrel <c>Server</c> response header (CWE-200)</item>
///   <item><c>X-Content-Type-Options: nosniff</c> (MIME-type sniffing prevention)</item>
///   <item><c>X-Frame-Options: DENY</c> (clickjacking prevention)</item>
///   <item><c>Strict-Transport-Security</c> (HSTS, 1 year default)</item>
///   <item><c>Referrer-Policy: strict-origin-when-cross-origin</c></item>
///   <item><c>Permissions-Policy</c> (browser feature restrictions)</item>
///   <item><c>Cross-Origin-Opener-Policy: same-origin</c> (Spectre mitigation)</item>
///   <item><c>Cross-Origin-Resource-Policy: same-origin</c></item>
///   <item><c>X-XSS-Protection: 0</c> (disables legacy XSS auditor)</item>
/// </list>
/// <para>
/// Standards: OWASP Secure Headers Project, OWASP ASVS V14.4, ISO 27001 A.8.9.
/// </para>
/// <para>
/// Call <c>app.UseGranitSecurityHeaders()</c> in <c>Program.cs</c>
/// <b>after</b> exception handling and <b>before</b> routing.
/// </para>
/// </remarks>
public sealed class GranitHttpSecurityModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Builder.AddGranitHttpSecurity();
}
