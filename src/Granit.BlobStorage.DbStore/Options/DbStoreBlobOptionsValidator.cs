using Microsoft.Extensions.Options;

namespace Granit.BlobStorage.DbStore.Options;

/// <summary>
/// Validates <see cref="DbStoreBlobOptions"/> at startup.
/// </summary>
internal sealed class DbStoreBlobOptionsValidator : IValidateOptions<DbStoreBlobOptions>
{
    public ValidateOptionsResult Validate(string? name, DbStoreBlobOptions options)
    {
        if (options.MaxBlobSizeBytes <= 0)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(DbStoreBlobOptions.MaxBlobSizeBytes)} must be greater than zero.");
        }

        return ValidateOptionsResult.Success;
    }
}
