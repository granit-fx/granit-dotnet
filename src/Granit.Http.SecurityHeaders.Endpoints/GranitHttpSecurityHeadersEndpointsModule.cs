using Granit.Authorization;
using Granit.Diagnostics.Endpoints;
using Granit.Modularity;
using Granit.Validation;

namespace Granit.Http.SecurityHeaders.Endpoints;

/// <summary>
/// Granit module for the CSP audit endpoint. Adds a single
/// permission-gated HTTP endpoint that returns the effective per-route
/// <c>Content-Security-Policy</c> snapshot for security audit consumers.
/// </summary>
/// <remarks>
/// Reuses <c>DiagnosticsPermissions.Monitoring.Read</c> — no new permission,
/// no new localisation keys.
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitDiagnosticsEndpointsModule),
    typeof(GranitHttpSecurityHeadersModule),
    typeof(GranitValidationModule))]
public sealed class GranitHttpSecurityHeadersEndpointsModule : GranitModule;
