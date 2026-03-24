using Granit.Modularity;
using Granit.Users;

namespace Granit.Authentication.OpenIddict;

/// <summary>
/// Granit module that registers OpenIddict token validation for resource servers.
/// Validates JWT/reference tokens from a remote Granit OpenIddict server without
/// embedding the full OIDC stack.
/// </summary>
public sealed class GranitAuthenticationOpenIddictModule : GranitModule;
