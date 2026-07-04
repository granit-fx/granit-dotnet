using Granit.Privacy.DataExport.Security;
using Microsoft.Extensions.Hosting;

namespace Granit.Privacy.BlobStorage.Internal;

/// <summary>
/// Fail-fast guard that aborts host startup whenever the resolved
/// <see cref="IExportContentEncryptor"/> is the in-memory ephemeral default and the
/// hosting environment is not <c>Development</c>.
/// </summary>
/// <remarks>
/// <para>
/// The assembled SAR package is application-layer encrypted before upload. With the
/// ephemeral default the key lives only in the current process, so a package encrypted by
/// one replica cannot be decrypted by another (or by the same replica after a restart) —
/// the subject's download would fail. More importantly, leaving the default unreplaced in
/// a real deployment is a silent invitation to disable encryption; making startup fail
/// closed forces an explicit, auditable choice: register a shared-state (Vault-backed)
/// encryptor, or run in <c>Development</c> for local-only work.
/// </para>
/// <para>
/// This mirrors <see cref="EphemeralExportHmacSignerStartupGuard"/> so the confidentiality
/// and integrity primitives share one operational posture: neither the export HMAC key nor
/// the export encryption key may be an ephemeral in-process key outside Development.
/// </para>
/// </remarks>
internal sealed class EphemeralExportContentEncryptorStartupGuard(
    IExportContentEncryptor encryptor,
    IHostEnvironment environment) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (environment.IsDevelopment())
        {
            return Task.CompletedTask;
        }

        if (encryptor is EphemeralExportContentEncryptor)
        {
            throw new InvalidOperationException(
                "Granit.Privacy.BlobStorage refuses to start in environment "
                + $"'{environment.EnvironmentName}' with the default "
                + $"{nameof(EphemeralExportContentEncryptor)} — its AES key lives only in the "
                + "current process, so multi-replica deployments and restarts cannot decrypt "
                + "SAR packages, and shipping personal-data exports with a throwaway key fails "
                + "GDPR Art. 32. Register a shared-state "
                + $"{nameof(IExportContentEncryptor)} (Vault-backed) before host startup, or "
                + "set the environment to Development for local-only runs.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
