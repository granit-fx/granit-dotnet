using System;
using System.IO;
using Granit.Browsing.Exceptions;
using Granit.Browsing.Sandbox;
using Microsoft.Extensions.Hosting;

namespace Granit.Browsing.PuppeteerSharp.Internal;

/// <summary>
/// Validates the configured <c>ChromiumExecutablePath</c> against
/// <see cref="IBrowserSandboxProfile.AllowedExecutablePathPrefix"/>, preventing the
/// provider from spawning a weaponised Chromium binary out of a writable location.
/// </summary>
internal static class PuppeteerExecutablePathValidator
{
    /// <summary>
    /// Resolves <paramref name="executablePath"/> to its full path and ensures it sits
    /// under <paramref name="allowedPrefix"/>. No-op when either argument is
    /// <c>null</c> or empty. When <paramref name="hostEnvironment"/> reports production
    /// AND an explicit executable override is set without an allowlist prefix, the
    /// caller is refused: production deploys MUST vet the binary location.
    /// </summary>
    /// <exception cref="SandboxViolationException">
    /// When the resolved path is outside the allowlisted prefix, or when no prefix is
    /// configured and the host is running in production.
    /// </exception>
    public static string? Validate(string? executablePath, string? allowedPrefix, IHostEnvironment? hostEnvironment = null)
    {
        if (string.IsNullOrEmpty(executablePath))
        {
            return null;
        }

        string resolved = Path.GetFullPath(executablePath);

        if (string.IsNullOrEmpty(allowedPrefix))
        {
            if (hostEnvironment is not null && hostEnvironment.IsProduction())
            {
                throw new SandboxViolationException(
                    SandboxViolationKind.ExecutablePathRejected,
                    $"IBrowserSandboxProfile.AllowedExecutablePathPrefix must be set when overriding ChromiumExecutablePath ('{resolved}') in production.");
            }
            return resolved;
        }

        string prefix = Path.GetFullPath(allowedPrefix);
        if (!resolved.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new SandboxViolationException(
                SandboxViolationKind.ExecutablePathRejected,
                $"Chromium executable '{resolved}' is outside the sandbox-allowed prefix '{prefix}'.");
        }

        return resolved;
    }
}
