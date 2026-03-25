using Microsoft.Extensions.Options;

namespace Granit.BlobStorage.Database.Options;

/// <summary>
/// Validates <see cref="DatabaseBlobOptions"/> at startup.
/// </summary>
internal sealed class DatabaseBlobOptionsValidator : IValidateOptions<DatabaseBlobOptions>
{
    public ValidateOptionsResult Validate(string? name, DatabaseBlobOptions options)
    {
        if (options.MaxBlobSizeBytes <= 0)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(DatabaseBlobOptions.MaxBlobSizeBytes)} must be greater than zero.");
        }

        return ValidateOptionsResult.Success;
    }
}
