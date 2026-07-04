using Granit.Browsing.Exceptions;
using Microsoft.Extensions.Hosting;

namespace Granit.Browsing.Sandbox;

/// <summary>
/// Validates a provider-configured browser executable path against
/// <see cref="IBrowserSandboxProfile.AllowedExecutablePathPrefix"/>, preventing any
/// <c>Granit.Browsing.*</c> provider from spawning a weaponised browser binary out of a
/// writable location.
/// </summary>
internal static class BrowserExecutablePathValidator
{
    /// <summary>
    /// Resolves <paramref name="executablePath"/> to its full path and ensures it sits
    /// under <paramref name="allowedPrefix"/>. No-op when either argument is
    /// <c>null</c> or empty. When <paramref name="hostEnvironment"/> reports production
    /// AND an explicit executable override is set without an allowlist prefix, the
    /// caller is refused: production deploys MUST vet the binary location.
    /// </summary>
    /// <param name="executablePath">The provider-configured executable path, or <c>null</c>/empty to use the provider default.</param>
    /// <param name="allowedPrefix">The sandbox-allowed prefix the resolved path must sit under, or <c>null</c>/empty to allow any location outside production.</param>
    /// <param name="executablePathOptionName">The provider option name reported in the production-refusal message (e.g. <c>ExecutablePath</c>, <c>ChromiumExecutablePath</c>).</param>
    /// <param name="hostEnvironment">The host environment used to enforce the production allowlist requirement, or <c>null</c> to skip that check.</param>
    /// <exception cref="SandboxViolationException">
    /// When the resolved path is outside the allowlisted prefix, or when no prefix is
    /// configured and the host is running in production.
    /// </exception>
    public static string? Validate(
        string? executablePath,
        string? allowedPrefix,
        string executablePathOptionName,
        IHostEnvironment? hostEnvironment = null)
    {
        if (string.IsNullOrEmpty(executablePath))
        {
            return null;
        }

        string resolved = Path.GetFullPath(executablePath);

        if (string.IsNullOrEmpty(allowedPrefix))
        {
            if (hostEnvironment?.IsProduction() == true)
            {
                throw new SandboxViolationException(
                    SandboxViolationKind.ExecutablePathRejected,
                    $"IBrowserSandboxProfile.AllowedExecutablePathPrefix must be set when overriding {executablePathOptionName} ('{resolved}') in production.");
            }
            return resolved;
        }

        string prefix = Path.GetFullPath(allowedPrefix);
        if (!resolved.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new SandboxViolationException(
                SandboxViolationKind.ExecutablePathRejected,
                $"Browser executable '{resolved}' is outside the sandbox-allowed prefix '{prefix}'.");
        }

        return resolved;
    }
}
