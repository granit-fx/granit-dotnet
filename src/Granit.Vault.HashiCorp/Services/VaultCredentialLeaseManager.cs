using Granit.Diagnostics;
using Granit.Vault.HashiCorp.Options;
using Granit.Vault.Internal;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VaultSharp;
using VaultSharp.V1.Commons;
using VaultSharp.V1.SecretsEngines;
using VaultSharp.V1.SystemBackend;

namespace Granit.Vault.HashiCorp.Services;

/// <summary>
/// Background service that manages the lifecycle of dynamic
/// PostgreSQL credentials via HashiCorp Vault Database Engine.
/// </summary>
internal sealed partial class VaultCredentialLeaseManager(
    IVaultClient vaultClient,
    IOptions<HashiCorpVaultOptions> options,
    ILogger<VaultCredentialLeaseManager> logger) : BackgroundService, IDatabaseCredentialProvider
{
    private readonly HashiCorpVaultOptions _options = options.Value;
    private readonly ZeroizingCredentialStore _store = new();

    private volatile string _leaseId = string.Empty;
    private volatile int _leaseDurationSeconds;

    public string Username => _store.Username;
    public string Password => _store.Password;
    public bool IsReady => _store.IsReady;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStartingCredentialManager();

        await ObtainCredentialsAsync(stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            var renewalDelay = TimeSpan.FromSeconds(
                _leaseDurationSeconds * _options.LeaseRenewalThreshold);

            LogNextRenewalIn(logger, renewalDelay);

            await Task.Delay(renewalDelay, stoppingToken).ConfigureAwait(false);

            try
            {
                await RenewLeaseAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogLeaseRenewalFailed(ex, _leaseId);

                await ObtainCredentialsAsync(stoppingToken).ConfigureAwait(false);
            }
        }

        LogStoppingCredentialManager();
    }

    private async Task ObtainCredentialsAsync(CancellationToken cancellationToken)
    {
        string path = $"{_options.DatabaseMountPoint}/creds/{_options.DatabaseRoleName}";
        LogObtainingCredentials(logger, path);

        // VaultSharp API does not expose cancellation — WaitAsync provides a defensive timeout.
        Secret<UsernamePasswordCredentials> secret = await vaultClient.V1.Secrets.Database.GetCredentialsAsync(
            _options.DatabaseRoleName,
            mountPoint: _options.DatabaseMountPoint).WaitAsync(cancellationToken).ConfigureAwait(false);

        _store.Apply(secret.Data.Username, secret.Data.Password);
        _leaseId = secret.LeaseId;
        _leaseDurationSeconds = secret.LeaseDurationSeconds;

        LogCredentialsObtained(logger, LogRedaction.Username(secret.Data.Username), _leaseDurationSeconds);
    }

    private async Task RenewLeaseAsync(CancellationToken cancellationToken)
    {
        LogRenewingLease(logger, _leaseId);

        // VaultSharp API does not expose cancellation — WaitAsync provides a defensive timeout.
        Secret<RenewedLease> renewed = await vaultClient.V1.System.RenewLeaseAsync(
            _leaseId,
            _leaseDurationSeconds).WaitAsync(cancellationToken).ConfigureAwait(false);

        _leaseDurationSeconds = renewed.LeaseDurationSeconds;

        LogLeaseRenewed(logger, _leaseId, _leaseDurationSeconds);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting Vault dynamic credential manager")]
    private partial void LogStartingCredentialManager();

    [LoggerMessage(Level = LogLevel.Information, Message = "Stopping Vault dynamic credential manager")]
    private partial void LogStoppingCredentialManager();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Lease renewal failed for {LeaseId}, obtaining new credentials")]
    private partial void LogLeaseRenewalFailed(Exception exception, string leaseId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Next lease renewal in {Delay}")]
    private static partial void LogNextRenewalIn(ILogger logger, TimeSpan delay);

    [LoggerMessage(Level = LogLevel.Information, Message = "Obtaining dynamic credentials from {Path}")]
    private static partial void LogObtainingCredentials(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Dynamic credentials obtained: user={RedactedUsername}, TTL={Ttl}s")]
    private static partial void LogCredentialsObtained(ILogger logger, string redactedUsername, int ttl);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Renewing lease {LeaseId}")]
    private static partial void LogRenewingLease(ILogger logger, string leaseId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Lease {LeaseId} renewed, new TTL={Ttl}s")]
    private static partial void LogLeaseRenewed(ILogger logger, string leaseId, int ttl);
}
