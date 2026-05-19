namespace Granit.Browsing.Sandbox;

/// <summary>
/// Default <see cref="IBrowserSandboxProfile"/> registered when the host does not
/// provide its own — HTTPS-only, no private networks, CSP forced, console redacted,
/// 30 s render cap. Opt-out, not opt-in.
/// </summary>
internal sealed class DefaultSandboxProfile : IBrowserSandboxProfile
{
    public bool DisableJavaScript => false;

    public bool BlockNetworkRequests => false;

    public bool DisableImages => false;

    public IReadOnlyList<string>? BlockedUrlPatterns => null;

    public IReadOnlyList<string> AllowedSchemes { get; } = ["https"];

    public bool BlockPrivateNetworks => true;

    public IReadOnlyList<string>? AllowedHostPatterns => null;

    public IReadOnlyList<string>? DeniedHostPatterns => null;

    public bool ForceCsp => true;

    public bool RedactConsoleMessages => true;

    public TimeSpan? MaxRenderDuration { get; } = TimeSpan.FromSeconds(30);

    public string? AllowedExecutablePathPrefix => null;
}
