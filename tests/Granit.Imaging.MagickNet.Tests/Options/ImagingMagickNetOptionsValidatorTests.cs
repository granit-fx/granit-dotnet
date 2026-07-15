using Granit.Imaging.MagickNet.Options;
using Shouldly;
using Xunit;

namespace Granit.Imaging.MagickNet.Tests.Options;

public sealed class ImagingMagickNetOptionsValidatorTests
{
    private readonly ImagingMagickNetOptionsValidator _validator = new();

    [Fact]
    public void Defaults_are_valid() =>
        _validator.Validate(null, new ImagingMagickNetOptions()).Succeeded.ShouldBeTrue();

    [Fact]
    public void Zero_values_are_valid_and_mean_ImageMagick_default()
    {
        ImagingMagickNetOptions options = new()
        {
            MaxMemoryBytes = 0,
            MaxWidthPixels = 0,
            MaxHeightPixels = 0,
            MaxListLength = 0,
            MaxInputBytes = 0,
        };

        _validator.Validate(null, options).Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData(-1L, 16384, 16384, 32, 52_428_800L)]            // negative memory
    [InlineData(268_435_456L, -1, 16384, 32, 52_428_800L)]      // negative width
    [InlineData(268_435_456L, 2_000_001, 16384, 32, 52_428_800L)] // width over ceiling
    [InlineData(268_435_456L, 16384, -1, 32, 52_428_800L)]      // negative height
    [InlineData(268_435_456L, 16384, 2_000_001, 32, 52_428_800L)] // height over ceiling
    [InlineData(268_435_456L, 16384, 16384, -1, 52_428_800L)]   // negative list length
    [InlineData(268_435_456L, 16384, 16384, 32, -1L)]           // negative input bytes
    [InlineData(268_435_456L, 16384, 16384, 32, 2_147_483_649L * 2)] // input bytes over 2 GiB ceiling
    public void Invalid_limits_fail(long maxMemory, int maxWidth, int maxHeight, int maxList, long maxInput)
    {
        ImagingMagickNetOptions options = new()
        {
            MaxMemoryBytes = maxMemory,
            MaxWidthPixels = maxWidth,
            MaxHeightPixels = maxHeight,
            MaxListLength = maxList,
            MaxInputBytes = maxInput,
        };

        _validator.Validate(null, options).Failed.ShouldBeTrue();
    }
}
