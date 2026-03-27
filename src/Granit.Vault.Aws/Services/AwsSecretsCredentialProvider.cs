using System.Text.Json;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Granit.Diagnostics;
using Granit.Vault.Aws.Diagnostics;
using Granit.Vault.Aws.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Vault.Aws.Services;

/// <summary>
/// Background service that reads database credentials from AWS Secrets Manager
/// and polls for rotation changes.
/// </summary>
internal sealed partial class AwsSecretsCredentialProvider(
    IAmazonSecretsManager secretsManager,
    IOptions<AwsVaultOptions> options,
    ILogger<AwsSecretsCredentialProvider> logger) : BackgroundService, IDatabaseCredentialProvider
{
    private readonly AwsVaultOptions _options = options.Value;

    private volatile string _username = string.Empty;
    private volatile string _password = string.Empty;
    private volatile string _versionId = string.Empty;

    /// <inheritdoc />
    public string Username => _username;

    /// <inheritdoc />
    public string Password => _password;

    /// <inheritdoc />
    public bool IsReady => !string.IsNullOrEmpty(_username);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrEmpty(_options.DatabaseSecretArn))
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
        LogObtainingCredentials(_options.DatabaseSecretArn!);

        using System.Diagnostics.Activity? activity = VaultAwsActivitySource.Source.StartActivity(
            VaultAwsActivitySource.Operations.SecretsObtain);

        GetSecretValueRequest request = new() { SecretId = _options.DatabaseSecretArn };

        GetSecretValueResponse response = await secretsManager
            .GetSecretValueAsync(request, cancellationToken)
            .ConfigureAwait(false);

        ApplySecret(response);
    }

    private async Task CheckForRotationAsync(CancellationToken cancellationToken)
    {
        try
        {
            using System.Diagnostics.Activity? activity = VaultAwsActivitySource.Source.StartActivity(
                VaultAwsActivitySource.Operations.SecretsCheck);

            DescribeSecretRequest describeRequest = new() { SecretId = _options.DatabaseSecretArn };

            DescribeSecretResponse describeResponse = await secretsManager
                .DescribeSecretAsync(describeRequest, cancellationToken)
                .ConfigureAwait(false);

            // Check if the AWSCURRENT version has changed
            string? currentVersion = describeResponse.VersionIdsToStages?
                .FirstOrDefault(kvp => kvp.Value.Contains("AWSCURRENT")).Key;

            if (currentVersion is not null && currentVersion != _versionId)
            {
                LogRotationDetected(currentVersion, _versionId);
                await ObtainCredentialsAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            LogRotationCheckFailed(ex);
        }
    }

    private void ApplySecret(GetSecretValueResponse response)
    {
        using var doc = JsonDocument.Parse(response.SecretString);
        JsonElement root = doc.RootElement;

        _username = root.GetProperty("username").GetString() ?? string.Empty;
        _password = root.GetProperty("password").GetString() ?? string.Empty;
        _versionId = response.VersionId;

        LogCredentialsObtained(LogRedaction.Username(_username), _versionId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting AWS Secrets Manager credential manager")]
    private partial void LogStartingCredentialManager();

    [LoggerMessage(Level = LogLevel.Information, Message = "Stopping AWS Secrets Manager credential manager")]
    private partial void LogStoppingCredentialManager();

    [LoggerMessage(Level = LogLevel.Information, Message = "No DatabaseSecretArn configured, credential manager disabled")]
    private partial void LogNoDatabaseSecret();

    [LoggerMessage(Level = LogLevel.Information, Message = "Obtaining credentials from {SecretArn}")]
    private partial void LogObtainingCredentials(string secretArn);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Credentials obtained: user={RedactedUsername}, version={VersionId}")]
    private partial void LogCredentialsObtained(string redactedUsername, string versionId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Next rotation check in {Interval}")]
    private partial void LogNextCheckIn(TimeSpan interval);

    [LoggerMessage(Level = LogLevel.Information, Message = "Secret rotation detected: new version {NewVersion}, previous {OldVersion}")]
    private partial void LogRotationDetected(string newVersion, string oldVersion);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Secret rotation check failed")]
    private partial void LogRotationCheckFailed(Exception exception);
}
