using Granit.Modularity;

namespace Granit.Security;

/// <summary>
/// Granit module providing security abstractions (<see cref="ICurrentUserService"/>).
/// No services are registered here — use <c>GranitJwtBearerModule</c> or
/// <c>GranitAuthenticationKeycloakModule</c> for the full implementation.
/// </summary>
public sealed class GranitSecurityModule : GranitModule;
