using System.Diagnostics;

#pragma warning disable GRSEC003 // Constants define activity operation names containing "token" — not actual secrets

namespace Granit.Oidc.TokenManagement.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Oidc.TokenManagement distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class TokenManagementActivitySource
{
    /// <summary>The name of the Granit.Oidc.TokenManagement <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.Oidc.TokenManagement";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────

    internal const string RequestToken = "token-management.request-token";
    internal const string RevokeToken = "token-management.revoke-token";
    internal const string CacheLookup = "token-management.cache-lookup";
}

#pragma warning restore GRSEC003
