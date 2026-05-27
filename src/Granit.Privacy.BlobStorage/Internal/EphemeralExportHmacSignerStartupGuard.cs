using Granit.Privacy.DataExport.Security;
using Microsoft.Extensions.Hosting;

namespace Granit.Privacy.BlobStorage.Internal;

/// <summary>
/// Fail-fast guard that aborts host startup whenever the resolved
/// <see cref="IExportHmacSigner"/> is the in-memory ephemeral default and the
/// hosting environment is not <c>Development</c>.
/// </summary>
/// <remarks>
/// <para>
/// The ephemeral signer generates a fresh key per process; in any multi-replica
/// or restart-prone deployment a shard signed by one process fails verification
/// in the next, silently corrupting personal-data exports. Hosts running outside
/// <c>Development</c> MUST register a shared-state signer (Vault-backed) before
/// the host starts.
/// </para>
/// <para>
/// Implemented as a <see cref="BackgroundService"/>-style hosted service so the
/// check runs at start, before any HTTP request hits the export endpoints.
/// </para>
/// </remarks>
internal sealed class EphemeralExportHmacSignerStartupGuard(
    IExportHmacSigner signer,
    IHostEnvironment environment) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (environment.IsDevelopment())
        {
            return Task.CompletedTask;
        }

        if (signer is EphemeralExportHmacSigner)
        {
            throw new InvalidOperationException(
                $"Granit.Privacy.BlobStorage refuses to start in environment "
                + $"'{environment.EnvironmentName}' with the default "
                + $"{nameof(EphemeralExportHmacSigner)} — its key lives only in the "
                + "current process, so multi-replica deployments and restarts will "
                + "silently fail shard manifest verification. Register a shared-state "
                + $"{nameof(IExportHmacSigner)} (Vault-backed) before host startup, or "
                + "set the environment to Development for local-only runs.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
