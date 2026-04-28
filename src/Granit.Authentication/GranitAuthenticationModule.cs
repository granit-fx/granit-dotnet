using Granit.Modularity;

namespace Granit.Authentication;

/// <summary>
/// Granit base module for cross-scheme authentication primitives.
/// Hosts shared claims transformations (e.g. role-claim normalization) consumed by
/// Granit.Authentication.* and Granit.OpenIddict.* resource-server packages so that
/// the framework's "<see cref="System.Security.Claims.ClaimTypes.Role"/>" contract
/// (read by <c>Granit.Authorization.PermissionChecker</c>) holds across schemes.
/// </summary>
public sealed class GranitAuthenticationModule : GranitModule;
