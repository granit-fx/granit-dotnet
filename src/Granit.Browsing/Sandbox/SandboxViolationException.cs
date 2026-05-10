using System;

namespace Granit.Browsing.Sandbox;

/// <summary>
/// Thrown by providers and the request router when a sandbox rule blocks an operation.
/// </summary>
public sealed class SandboxViolationException : Exception
{
    /// <summary>The category of the violation.</summary>
    public SandboxViolationKind Kind { get; }

    /// <summary>Creates a new <see cref="SandboxViolationException"/>.</summary>
    public SandboxViolationException(SandboxViolationKind kind, string message)
        : base(message)
    {
        Kind = kind;
    }

    /// <summary>Creates a new <see cref="SandboxViolationException"/> wrapping an inner exception.</summary>
    public SandboxViolationException(SandboxViolationKind kind, string message, Exception inner)
        : base(message, inner)
    {
        Kind = kind;
    }
}

/// <summary>Categorises a <see cref="SandboxViolationException"/>.</summary>
public enum SandboxViolationKind
{
    /// <summary>The URL scheme is not in <see cref="IBrowserSandboxProfile.AllowedSchemes"/>.</summary>
    SchemeNotAllowed,

    /// <summary>The host is denied by <see cref="IBrowserSandboxProfile.DeniedHostPatterns"/> or <see cref="IBrowserSandboxProfile.BlockedUrlPatterns"/>.</summary>
    HostBlocked,

    /// <summary>The host resolves to a private / loopback / link-local / cloud-metadata address while <see cref="IBrowserSandboxProfile.BlockPrivateNetworks"/> is set.</summary>
    PrivateNetworkBlocked,

    /// <summary>Wraps an <see cref="Granit.Http.Security.UrlSafetyResult"/> violation surfaced by <see cref="Granit.Http.Security.IUrlSafetyValidator"/>.</summary>
    UrlSafetyViolation,

    /// <summary>A caller attempted to bypass CSP while <see cref="IBrowserSandboxProfile.ForceCsp"/> is set.</summary>
    CspBypassDenied,

    /// <summary>A caller attempted to inject script content the sandbox forbids.</summary>
    ScriptInjectionDenied,

    /// <summary>The provider's executable path is outside <see cref="IBrowserSandboxProfile.AllowedExecutablePathPrefix"/>.</summary>
    ExecutablePathRejected,

    /// <summary>A privileged provider flag (<c>--no-sandbox</c>, <c>--disable-setuid-sandbox</c>, <c>DisableSandbox</c>) was refused outside an opt-in container context.</summary>
    PrivilegedFlagRefused,

    /// <summary>A render operation exceeded <see cref="IBrowserSandboxProfile.MaxRenderDuration"/>.</summary>
    RenderTimeoutExceeded,

    /// <summary>A console message containing a likely secret was redacted.</summary>
    ConsoleSecretRedacted,
}
