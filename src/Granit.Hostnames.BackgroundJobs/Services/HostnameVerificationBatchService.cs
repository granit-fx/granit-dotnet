using Granit.Hostnames.Contracts;
using Granit.Hostnames.Domain;
using Microsoft.Extensions.Logging;

namespace Granit.Hostnames.BackgroundJobs.Services;

/// <summary>
/// Fetches hostnames due for a DNS check and runs the verifier against each one.
/// Driven by per-domain <c>NextCheckAt</c> / <c>FailedCheckCount</c> backoff
/// so DNS resolvers are not overwhelmed by repeated checks on misconfigured domains.
/// </summary>
public sealed partial class HostnameVerificationBatchService(
    IManagedHostnameReader reader,
    IManagedHostnameWriter writer,
    IHostnameVerifier verifier,
    TimeProvider timeProvider,
    ILogger<HostnameVerificationBatchService> logger)
{
    private readonly IManagedHostnameReader _reader = reader;
    private readonly IManagedHostnameWriter _writer = writer;
    private readonly IHostnameVerifier _verifier = verifier;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<HostnameVerificationBatchService> _logger = logger;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();

        IReadOnlyList<ManagedHostname> due = await _reader
            .ListDueForVerificationAsync(now, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (due.Count == 0)
        {
            LogNoDueHostnames();
            return;
        }

        LogStartingBatch(due.Count);

        foreach (ManagedHostname hostname in due)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await VerifyOneAsync(hostname, now, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task VerifyOneAsync(
        ManagedHostname hostname,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        try
        {
            HostnameVerificationResult result = await _verifier
                .VerifyAsync(hostname, cancellationToken)
                .ConfigureAwait(false);

            if (result.IsVerified)
            {
                hostname.MarkVerified(now);
                LogHostnameVerified(hostname.Host.Value);
            }
            else
            {
                hostname.MarkFailed(result.Conflicts, now);
                LogHostnameFailed(hostname.Host.Value, hostname.FailedCheckCount, hostname.NextCheckAt);
            }

            await _writer.UpdateAsync(hostname, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogVerificationError(hostname.Host.Value, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Hostname verification batch: no hostnames due for check.")]
    private partial void LogNoDueHostnames();

    [LoggerMessage(Level = LogLevel.Information, Message = "Hostname verification batch: checking {Count} hostname(s).")]
    private partial void LogStartingBatch(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Hostname '{Host}' verified successfully.")]
    private partial void LogHostnameVerified(string host);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Hostname '{Host}' verification failed (attempt #{FailedCheckCount}); next check at {NextCheckAt}.")]
    private partial void LogHostnameFailed(string host, int failedCheckCount, DateTimeOffset? nextCheckAt);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error verifying hostname '{Host}'.")]
    private partial void LogVerificationError(string host, Exception ex);
}
