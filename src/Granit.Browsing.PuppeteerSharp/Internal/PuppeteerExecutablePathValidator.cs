using System;
using System.IO;
using Granit.Browsing.Exceptions;
using Granit.Browsing.Sandbox;

namespace Granit.Browsing.PuppeteerSharp.Internal;

/// <summary>
/// Validates the configured <c>ChromiumExecutablePath</c> against
/// <see cref="IBrowserSandboxProfile.AllowedExecutablePathPrefix"/> to prevent the
/// provider from spawning a weaponised Chromium binary located in a writable directory.
/// </summary>
internal static class PuppeteerExecutablePathValidator
{
    /// <summary>
    /// Resolves <paramref name="executablePath"/> to its full path and ensures it sits
    /// under <paramref name="allowedPrefix"/>. No-op when either argument is
    /// <c>null</c> or empty.
    /// </summary>
    /// <exception cref="SandboxViolationException">
    /// When the resolved path is outside the allowlisted prefix.
    /// </exception>
    public static string? Validate(string? executablePath, string? allowedPrefix)
    {
        if (string.IsNullOrEmpty(executablePath))
        {
            return null;
        }

        string resolved = Path.GetFullPath(executablePath);

        if (string.IsNullOrEmpty(allowedPrefix))
        {
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
