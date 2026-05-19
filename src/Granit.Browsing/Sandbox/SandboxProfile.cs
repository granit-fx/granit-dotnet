namespace Granit.Browsing.Sandbox;

/// <summary>
/// Concrete <see cref="IBrowserSandboxProfile"/> with ergonomic <c>init</c>-only
/// properties and the same deny-by-default values as <c>DefaultSandboxProfile</c>.
/// </summary>
/// <remarks>
/// Use this record when a host wants to tweak one or two settings while inheriting the
/// framework's safe defaults:
/// <code>
/// services.AddSingleton&lt;IBrowserSandboxProfile&gt;(new SandboxProfile
/// {
///     AllowedHostPatterns = ["**.partner.example.com"],
///     MaxRenderDuration = TimeSpan.FromSeconds(60),
/// });
/// </code>
/// </remarks>
public sealed record SandboxProfile : IBrowserSandboxProfile
{
    /// <inheritdoc/>
    public bool DisableJavaScript { get; init; }

    /// <inheritdoc/>
    public bool BlockNetworkRequests { get; init; }

    /// <inheritdoc/>
    public bool DisableImages { get; init; }

    /// <inheritdoc/>
    public IReadOnlyList<string>? BlockedUrlPatterns { get; init; }

    /// <inheritdoc/>
    public IReadOnlyList<string> AllowedSchemes { get; init; } = ["https"];

    /// <inheritdoc/>
    public bool BlockPrivateNetworks { get; init; } = true;

    /// <inheritdoc/>
    public IReadOnlyList<string>? AllowedHostPatterns { get; init; }

    /// <inheritdoc/>
    public IReadOnlyList<string>? DeniedHostPatterns { get; init; }

    /// <inheritdoc/>
    public bool ForceCsp { get; init; } = true;

    /// <inheritdoc/>
    public bool RedactConsoleMessages { get; init; } = true;

    /// <inheritdoc/>
    public TimeSpan? MaxRenderDuration { get; init; } = TimeSpan.FromSeconds(30);

    /// <inheritdoc/>
    public string? AllowedExecutablePathPrefix { get; init; }
}
