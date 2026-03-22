using Granit.Core.Modularity;
using Granit.Security;

namespace Granit.Authentication.OpenIddict;

/// <summary>
/// Granit module that registers OpenIddict token validation for resource servers.
/// Validates JWT/reference tokens from a remote Granit OpenIddict server without
/// embedding the full OIDC stack.
/// </summary>
[DependsOn(typeof(GranitSecurityModule))]
public sealed class GranitAuthenticationOpenIddictModule : GranitModule;
