using System;
using System.Collections.Generic;
using System.IO;
using Granit.Browsing.Sandbox;
using Microsoft.Extensions.Logging;

namespace Granit.Browsing.Pool;

/// <summary>
/// Refuses provider flags that disable the Chromium / Firefox sandbox unless the host
/// is running in an authorised container context as a non-root user with the explicit
/// opt-in environment variable set. Closes VULN-103.
/// </summary>
/// <remarks>
/// <para>
/// "Container" detection uses two pragmatic signals — the <c>/.dockerenv</c> marker file
/// and the presence of <c>docker</c>, <c>kubepods</c>, <c>containerd</c>, or
/// <c>podman</c> in <c>/proc/1/cgroup</c>. These cover Docker, Kubernetes, containerd,
/// and Podman; Windows containers, Firecracker microVMs, and bespoke runtimes are NOT
/// detected and will be refused. That tradeoff is deliberate: when in doubt, refuse.
/// </para>
/// <para>
/// Non-root detection uses the <c>USER</c> environment variable (or the value of
/// <see cref="Environment.UserName"/> as fallback). The check is best-effort — a host
/// determined to run as root in a privileged container can still bypass it by exporting
/// <c>USER=app</c>, but that is outside our threat model.
/// </para>
/// </remarks>
public static class PrivilegedFlagGuard
{
    /// <summary>Environment variable that must be set to <c>"1"</c> to opt in.</summary>
    public const string OptInEnvVar = "GRANIT_BROWSING_ALLOW_NO_SANDBOX";

    private static readonly string[] PrivilegedArgs =
    [
        "--no-sandbox",
        "--disable-setuid-sandbox",
    ];

    /// <summary>
    /// Throws <see cref="SandboxViolationException"/> with kind
    /// <see cref="SandboxViolationKind.PrivilegedFlagRefused"/> when a privileged
    /// configuration is requested outside the allowlisted context.
    /// </summary>
    /// <param name="disableSandbox">Whether the provider was asked to disable its sandbox.</param>
    /// <param name="extraArgs">Provider-supplied extra arguments to the browser process.</param>
    /// <param name="logger">Logger used to emit a warning when a refusal happens.</param>
    /// <param name="probe">Optional probe abstraction; defaults to <see cref="DefaultEnvironmentProbe"/>.</param>
    public static void EnsureSafe(
        bool disableSandbox,
        IReadOnlyList<string>? extraArgs,
        ILogger logger,
        IEnvironmentProbe? probe = null)
    {
        ArgumentNullException.ThrowIfNull(logger);

        string? offendingArg = null;
        if (extraArgs is not null)
        {
            foreach (string arg in extraArgs)
            {
                foreach (string privileged in PrivilegedArgs)
                {
                    if (arg.Contains(privileged, StringComparison.OrdinalIgnoreCase))
                    {
                        offendingArg = privileged;
                        break;
                    }
                }

                if (offendingArg is not null)
                {
                    break;
                }
            }
        }

        if (!disableSandbox && offendingArg is null)
        {
            return;
        }

        IEnvironmentProbe p = probe ?? DefaultEnvironmentProbe.Instance;

        bool optedIn = string.Equals(p.GetEnvironmentVariable(OptInEnvVar), "1", StringComparison.Ordinal);
        bool inContainer = p.IsRunningInContainer();
        bool nonRoot = !p.IsRunningAsRoot();

        if (optedIn && inContainer && nonRoot)
        {
            logger.LogWarning(
                "Privileged browser flag '{Flag}' permitted: container={Container}, nonRoot={NonRoot}, optIn={OptIn}.",
                offendingArg ?? "DisableSandbox",
                inContainer,
                nonRoot,
                optedIn);
            return;
        }

        throw new SandboxViolationException(
            SandboxViolationKind.PrivilegedFlagRefused,
            $"Privileged browser flag '{offendingArg ?? "DisableSandbox"}' refused (container={inContainer}, nonRoot={nonRoot}, optIn={optedIn}). Set {OptInEnvVar}=1 in a non-root container to allow.");
    }
}

/// <summary>Pluggable probe for container / privilege detection — enables unit tests.</summary>
public interface IEnvironmentProbe
{
    /// <summary>Returns the value of <paramref name="name"/> in the process environment.</summary>
    string? GetEnvironmentVariable(string name);

    /// <summary>Returns <c>true</c> when the process appears to run in a Linux container.</summary>
    bool IsRunningInContainer();

    /// <summary>Returns <c>true</c> when the process appears to run as root.</summary>
    bool IsRunningAsRoot();
}

/// <summary>Default probe that reads the real host environment.</summary>
public sealed class DefaultEnvironmentProbe : IEnvironmentProbe
{
    /// <summary>Singleton instance.</summary>
    public static DefaultEnvironmentProbe Instance { get; } = new();

    /// <inheritdoc/>
    public string? GetEnvironmentVariable(string name) =>
#pragma warning disable RS0030 // Process-environment access is intrinsic to PrivilegedFlagGuard: the opt-in lives in env at process boot, before IConfiguration is wired.
        Environment.GetEnvironmentVariable(name);
#pragma warning restore RS0030

    /// <inheritdoc/>
    public bool IsRunningInContainer()
    {
        if (File.Exists("/.dockerenv"))
        {
            return true;
        }

        try
        {
            if (!File.Exists("/proc/1/cgroup"))
            {
                return false;
            }

            string cgroup = File.ReadAllText("/proc/1/cgroup");
            return cgroup.Contains("docker", StringComparison.OrdinalIgnoreCase)
                || cgroup.Contains("kubepods", StringComparison.OrdinalIgnoreCase)
                || cgroup.Contains("containerd", StringComparison.OrdinalIgnoreCase)
                || cgroup.Contains("podman", StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public bool IsRunningAsRoot()
    {
#pragma warning disable RS0030 // See GetEnvironmentVariable rationale.
        string? user = Environment.GetEnvironmentVariable("USER")
            ?? Environment.UserName;
#pragma warning restore RS0030
        return string.Equals(user, "root", StringComparison.Ordinal);
    }
}
