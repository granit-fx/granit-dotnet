using System.Diagnostics;

namespace Granit.Bff.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Bff distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
#pragma warning disable GRSEC003 // Operation names contain "token" — refers to trace spans, not secrets
internal static class BffActivitySource
{
    /// <summary>The name of the Granit.Bff <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.Bff";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────

    internal const string Login = "bff.login";
    internal const string Callback = "bff.callback";
    internal const string Logout = "bff.logout";
    internal const string User = "bff.user";
    internal const string TokenRefresh = "bff.token-refresh";
    internal const string Proxy = "bff.proxy";
}
#pragma warning restore GRSEC003
