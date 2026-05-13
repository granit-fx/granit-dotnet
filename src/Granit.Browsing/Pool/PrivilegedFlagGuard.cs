using System;
using System.Collections.Generic;
using System.IO;
using Granit.Browsing.Exceptions;
using Granit.Browsing.Sandbox;
using Microsoft.Extensions.Logging;

namespace Granit.Browsing.Pool;

/// <summary>
/// Refuses provider flags that disable the Chromium / Firefox sandbox unless the host
/// is running in an authorised container context as a non-root user with the explicit
/// opt-in environment variable set.
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

    /// <summary>
    /// Flags refused unconditionally — these widen the renderer attack surface in ways
    /// that no container opt-in can mitigate (CSP / origin isolation off, debugger
    /// exposure, MITM-friendly proxies, arbitrary executable / data-dir relocation).
    /// </summary>
    private static readonly string[] AlwaysForbidden =
    [
        "--disable-web-security",
        "--allow-file-access-from-files",
        "--disable-features=IsolateOrigins",
        "--disable-site-isolation-trials",
        "--remote-debugging-port",
        "--remote-debugging-address",
        "--proxy-server",
        "--proxy-bypass-list",
        "--ignore-certificate-errors",
        "--user-data-dir",
    ];

    /// <summary>
    /// Flags that disable the platform sandbox. Refused unless the host is a non-root
    /// process inside a vetted container with the opt-in environment variable set.
    /// </summary>
    private static readonly string[] RequiresContainerOptIn =
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

        // 1. Always-forbidden flags — no opt-in path exists, refuse immediately. Each
        // entry is compared against the bare arg token first (everything before '='), and
        // also against the full arg when the forbidden entry itself contains an '=' (so
        // e.g. --disable-features=IsolateOrigins is matched precisely without rejecting
        // every --disable-features=* value Chromium ships).
        if (extraArgs is not null)
        {
            foreach (string arg in extraArgs)
            {
                string token = ExtractToken(arg);
                foreach (string forbidden in AlwaysForbidden)
                {
                    bool match = forbidden.Contains('=', StringComparison.Ordinal)
                        ? string.Equals(arg, forbidden, StringComparison.OrdinalIgnoreCase)
                        : TokenMatches(token, forbidden);
                    if (match)
                    {
                        throw new SandboxViolationException(
                            SandboxViolationKind.PrivilegedFlagRefused,
                            $"Privileged browser flag '{forbidden}' refused unconditionally — this flag widens the renderer attack surface and has no safe opt-in.");
                    }
                }
            }
        }

        // 2. Container-opt-in flags (sandbox disablers).
        string? offendingArg = null;
        if (extraArgs is not null)
        {
            foreach (string arg in extraArgs)
            {
                string token = ExtractToken(arg);
                foreach (string privileged in RequiresContainerOptIn)
                {
                    if (TokenMatches(token, privileged))
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

    /// <summary>
    /// Returns the argument token (everything before the first <c>=</c>) so an arg like
    /// <c>--proxy-server=http://...</c> is reduced to <c>--proxy-server</c>. Avoids
    /// false positives like <c>--js-flags=--no-sandbox</c> matching <c>--no-sandbox</c>.
    /// </summary>
    private static string ExtractToken(string arg)
    {
        int eq = arg.IndexOf('=');
        return eq < 0 ? arg : arg[..eq];
    }

    private static bool TokenMatches(string token, string forbidden) =>
        string.Equals(token, forbidden, StringComparison.OrdinalIgnoreCase);
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
        // 1. Docker marker file.
        if (File.Exists("/.dockerenv"))
        {
            return true;
        }

        // 2. Podman marker file.
        if (File.Exists("/run/.containerenv"))
        {
            return true;
        }

        // 3. systemd convention — every container manager sets the "container" env var.
#pragma warning disable RS0030 // Process-environment access is intrinsic to container detection at boot.
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("container")))
        {
            return true;
        }
#pragma warning restore RS0030

        // 4. cgroup v1 markers (Docker, kubepods, containerd, Podman) in /proc/1/cgroup.
        try
        {
            if (File.Exists("/proc/1/cgroup"))
            {
                string cgroup = File.ReadAllText("/proc/1/cgroup");
                if (cgroup.Contains("docker", StringComparison.OrdinalIgnoreCase)
                    || cgroup.Contains("kubepods", StringComparison.OrdinalIgnoreCase)
                    || cgroup.Contains("containerd", StringComparison.OrdinalIgnoreCase)
                    || cgroup.Contains("podman", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        // 5. cgroup v2 / hardened-namespace heuristic — when PID 1 is neither "init" nor
        // "systemd" (typical bare-metal hosts), this process was started by a container
        // runtime (Docker/Podman/k8s typically exec the app as PID 1).
        try
        {
            if (File.Exists("/proc/1/sched"))
            {
                using var reader = new StreamReader("/proc/1/sched");
                string? firstLine = reader.ReadLine();
                if (!string.IsNullOrEmpty(firstLine))
                {
                    // Format: "<comm> (<pid>, #threads: <n>)" — extract comm token.
                    int space = firstLine.IndexOf(' ');
                    string comm = space < 0 ? firstLine : firstLine[..space];
                    if (!string.Equals(comm, "init", StringComparison.Ordinal)
                        && !string.Equals(comm, "systemd", StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        return false;
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
