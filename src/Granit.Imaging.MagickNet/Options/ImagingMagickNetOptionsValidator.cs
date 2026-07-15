using Microsoft.Extensions.Options;

namespace Granit.Imaging.MagickNet.Options;

/// <summary>
/// Validates <see cref="ImagingMagickNetOptions"/> at startup (fail-fast on invalid limits).
/// </summary>
internal sealed class ImagingMagickNetOptionsValidator : IValidateOptions<ImagingMagickNetOptions>
{
    // Sanity ceilings guarding the (ulong) casts and absurd configuration typos.
    private const int DimensionCeilingPixels = 2_000_000;
    private const long InputBytesCeiling = 2L * 1024 * 1024 * 1024;

    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, ImagingMagickNetOptions options)
    {
        if (options.MaxMemoryBytes < 0)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.MaxMemoryBytes)} must be >= 0 (0 = ImageMagick default).");
        }

        if (options.MaxWidthPixels < 0 || options.MaxWidthPixels > DimensionCeilingPixels)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.MaxWidthPixels)} must be between 0 and {DimensionCeilingPixels} (0 = ImageMagick default).");
        }

        if (options.MaxHeightPixels < 0 || options.MaxHeightPixels > DimensionCeilingPixels)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.MaxHeightPixels)} must be between 0 and {DimensionCeilingPixels} (0 = ImageMagick default).");
        }

        if (options.MaxListLength < 0)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.MaxListLength)} must be >= 0 (0 = ImageMagick default).");
        }

        if (options.MaxInputBytes < 0 || options.MaxInputBytes > InputBytesCeiling)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.MaxInputBytes)} must be between 0 and {InputBytesCeiling} bytes (0 = disabled).");
        }

        return ValidateOptionsResult.Success;
    }
}
