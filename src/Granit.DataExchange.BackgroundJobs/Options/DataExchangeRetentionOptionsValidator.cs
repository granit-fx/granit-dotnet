using Microsoft.Extensions.Options;

namespace Granit.DataExchange.BackgroundJobs.Options;

/// <summary>
/// Validates <see cref="DataExchangeRetentionOptions"/> at startup.
/// </summary>
internal sealed class DataExchangeRetentionOptionsValidator : IValidateOptions<DataExchangeRetentionOptions>
{
    public ValidateOptionsResult Validate(string? name, DataExchangeRetentionOptions options)
    {
        if (options.ImportFileRetention <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail($"{nameof(options.ImportFileRetention)} must be a positive duration.");
        }

        if (options.ExportFileRetention <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail($"{nameof(options.ExportFileRetention)} must be a positive duration.");
        }

        if (options.JobRecordRetention <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail($"{nameof(options.JobRecordRetention)} must be a positive duration.");
        }

        if (options.StuckJobTimeout <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail($"{nameof(options.StuckJobTimeout)} must be a positive duration.");
        }

        if (options.SweepBatchSize is < 1 or > 10_000)
        {
            return ValidateOptionsResult.Fail($"{nameof(options.SweepBatchSize)} must be between 1 and 10000.");
        }

        return ValidateOptionsResult.Success;
    }
}
