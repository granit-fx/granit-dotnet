using System.Text.Json;
using Google.Cloud.SecretManager.V1;
using Granit.Diagnostics;
using Granit.Vault.GoogleCloud.Diagnostics;
using Granit.Vault.GoogleCloud.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Vault.GoogleCloud.Services;

/// <summary>
/// Background service that reads database credentials from Google Cloud Secret Manager
/// and polls for version changes.
/// </summary>
internal sealed partial class SecretManagerCredentialProvider(
    SecretManagerServiceClient secretManagerClient,
    IOptions<GoogleCloudVaultOptions> options,
    ILogger<SecretManagerCredentialProvider> logger) : BackgroundService, IDatabaseCredentialProvider
{
    private readonly GoogleCloudVaultOptions _options = options.Value;

    private volatile string _username = string.Empty;
    private volatile string _password = string.Empty;
    private volatile string _versionName = string.Empty;

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
        SecretVersionName secretVersionName = new(
            _options.ProjectId, _options.DatabaseSecretName, "latest");

        LogObtainingCredentials(secretVersionName.ToString());

        using System.Diagnostics.Activity? activity = VaultGoogleCloudActivitySource.Source.StartActivity(
            VaultGoogleCloudActivitySource.Operations.SecretsObtain);

        AccessSecretVersionResponse response = await secretManagerClient
            .AccessSecretVersionAsync(secretVersionName, cancellationToken)
            .ConfigureAwait(false);

        ApplySecret(response);
    }

    private async Task CheckForRotationAsync(CancellationToken cancellationToken)
    {
        try
        {
            using System.Diagnostics.Activity? activity = VaultGoogleCloudActivitySource.Source.StartActivity(
                VaultGoogleCloudActivitySource.Operations.SecretsCheck);

            SecretVersionName secretVersionName = new(
                _options.ProjectId, _options.DatabaseSecretName, "latest");

            AccessSecretVersionResponse response = await secretManagerClient
                .AccessSecretVersionAsync(secretVersionName, cancellationToken)
                .ConfigureAwait(false);

            if (response.Name != _versionName)
            {
                LogRotationDetected(response.Name, _versionName);
                ApplySecret(response);
            }
        }
        catch (Exception ex)
        {
            LogRotationCheckFailed(ex);
        }
    }

    private void ApplySecret(AccessSecretVersionResponse response)
    {
        string secretPayload = response.Payload.Data.ToStringUtf8();
        using var doc = JsonDocument.Parse(secretPayload);
        JsonElement root = doc.RootElement;

        _username = root.GetProperty("username").GetString() ?? string.Empty;
        _password = root.GetProperty("password").GetString() ?? string.Empty;
        _versionName = response.Name;

        LogCredentialsObtained(LogRedaction.Username(_username), _versionName);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting Secret Manager credential manager")]
    private partial void LogStartingCredentialManager();

    [LoggerMessage(Level = LogLevel.Information, Message = "Stopping Secret Manager credential manager")]
    private partial void LogStoppingCredentialManager();

    [LoggerMessage(Level = LogLevel.Information, Message = "No DatabaseSecretName configured, credential manager disabled")]
    private partial void LogNoDatabaseSecret();

    [LoggerMessage(Level = LogLevel.Information, Message = "Obtaining credentials from {SecretName}")]
    private partial void LogObtainingCredentials(string secretName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Credentials obtained: user={RedactedUsername}, version={VersionName}")]
    private partial void LogCredentialsObtained(string redactedUsername, string versionName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Next rotation check in {Interval}")]
    private partial void LogNextCheckIn(TimeSpan interval);

    [LoggerMessage(Level = LogLevel.Information, Message = "Secret rotation detected: new version {NewVersion}, previous {OldVersion}")]
    private partial void LogRotationDetected(string newVersion, string oldVersion);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Secret rotation check failed")]
    private partial void LogRotationCheckFailed(Exception exception);
}
