using System.Text.Json;
using Azure.Security.KeyVault.Secrets;
using Granit.Diagnostics;
using Granit.Vault.Azure.Diagnostics;
using Granit.Vault.Azure.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Vault.Azure.Services;

/// <summary>
/// Background service that reads database credentials from Azure Key Vault Secrets
/// and polls for version changes.
/// </summary>
internal sealed partial class AzureSecretsCredentialProvider(
    SecretClient secretClient,
    IOptions<AzureKeyVaultOptions> options,
    ILogger<AzureSecretsCredentialProvider> logger) : BackgroundService, IDatabaseCredentialProvider
{
    private readonly AzureKeyVaultOptions _options = options.Value;

    private volatile string _username = string.Empty;
    private volatile string _password = string.Empty;
    private volatile string _version = string.Empty;

    /// <inheritdoc />
    public string Username => _username;

    /// <inheritdoc />
    public string Password => _password;

    /// <inheritdoc />
    public bool IsReady => !string.IsNullOrEmpty(_username);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrEmpty(_options.DatabaseSecretName))
        {
            LogNoDatabaseSecret();
            return;
        }

        LogStartingCredentialManager();

        await ObtainCredentialsAsync(stoppingToken).ConfigureAwait(false);

        var pollInterval = TimeSpan.FromMinutes(_options.RotationCheckIntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            LogNextCheckIn(pollInterval);

            await Task.Delay(pollInterval, stoppingToken).ConfigureAwait(false);

            await CheckForRotationAsync(stoppingToken).ConfigureAwait(false);
        }

        LogStoppingCredentialManager();
    }

    private async Task ObtainCredentialsAsync(CancellationToken cancellationToken)
    {
        LogObtainingCredentials(_options.DatabaseSecretName!);

        using System.Diagnostics.Activity? activity = VaultAzureActivitySource.Source.StartActivity(
            VaultAzureActivitySource.Operations.AkvGetSecret);

        global::Azure.Response<KeyVaultSecret> response = await secretClient
            .GetSecretAsync(_options.DatabaseSecretName, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        ApplySecret(response.Value);
    }

    private async Task CheckForRotationAsync(CancellationToken cancellationToken)
    {
        try
        {
            using System.Diagnostics.Activity? activity = VaultAzureActivitySource.Source.StartActivity(
                VaultAzureActivitySource.Operations.AkvCheckRotation);

            global::Azure.Response<KeyVaultSecret> response = await secretClient
                .GetSecretAsync(_options.DatabaseSecretName, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            KeyVaultSecret secret = response.Value;
            string currentVersion = secret.Properties.Version ?? string.Empty;

            if (!string.IsNullOrEmpty(currentVersion) && currentVersion != _version)
            {
                LogRotationDetected(currentVersion, _version);
                ApplySecret(secret);
            }
        }
        catch (Exception ex)
        {
            LogRotationCheckFailed(ex);
        }
    }

    private void ApplySecret(KeyVaultSecret secret)
    {
        using var doc = JsonDocument.Parse(secret.Value);
        JsonElement root = doc.RootElement;

        _username = root.GetProperty("username").GetString() ?? string.Empty;
        _password = root.GetProperty("password").GetString() ?? string.Empty;
        _version = secret.Properties.Version ?? string.Empty;

        LogCredentialsObtained(LogRedaction.Username(_username), _version);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting Azure Key Vault Secrets credential manager")]
    private partial void LogStartingCredentialManager();

    [LoggerMessage(Level = LogLevel.Information, Message = "Stopping Azure Key Vault Secrets credential manager")]
    private partial void LogStoppingCredentialManager();

    [LoggerMessage(Level = LogLevel.Information, Message = "No DatabaseSecretName configured, credential manager disabled")]
    private partial void LogNoDatabaseSecret();

    [LoggerMessage(Level = LogLevel.Information, Message = "Obtaining credentials from {SecretName}")]
    private partial void LogObtainingCredentials(string secretName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Credentials obtained: user={RedactedUsername}, version={Version}")]
    private partial void LogCredentialsObtained(string redactedUsername, string version);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Next rotation check in {Interval}")]
    private partial void LogNextCheckIn(TimeSpan interval);

    [LoggerMessage(Level = LogLevel.Information, Message = "Secret rotation detected: new version {NewVersion}, previous {OldVersion}")]
    private partial void LogRotationDetected(string newVersion, string oldVersion);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Secret rotation check failed")]
    private partial void LogRotationCheckFailed(Exception exception);
}
