using Granit.Imaging.MagickNet.Internal;
using Granit.Imaging.MagickNet.Options;
using ImageMagick;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Imaging.MagickNet.Tests.Internal;

// ResourceLimits is process-global native state; both tests in this class mutate and
// restore it. xUnit runs tests of a single class sequentially, and no other test in
// this assembly touches the statics (application moved out of DI registration).
public sealed class MagickNetResourceLimitsInitializerTests
{
    [Fact]
    public async Task StartAsync_applies_configured_limits_to_ImageMagick()
    {
        ulong originalMemory = ResourceLimits.Memory;
        ulong originalWidth = ResourceLimits.Width;
        ulong originalHeight = ResourceLimits.Height;
        ulong originalListLength = ResourceLimits.ListLength;

        try
        {
            ImagingMagickNetOptions options = new()
            {
                MaxMemoryBytes = 128 * 1024 * 1024,
                MaxWidthPixels = 4096,
                MaxHeightPixels = 2048,
                MaxListLength = 8,
            };
            MagickNetResourceLimitsInitializer initializer = new(
                Microsoft.Extensions.Options.Options.Create(options),
                NullLogger<MagickNetResourceLimitsInitializer>.Instance);

            await initializer.StartAsync(CancellationToken.None);

            (ResourceLimits.Memory == 128UL * 1024 * 1024).ShouldBeTrue();
            (ResourceLimits.Width == 4096UL).ShouldBeTrue();
            (ResourceLimits.Height == 2048UL).ShouldBeTrue();
            (ResourceLimits.ListLength == 8UL).ShouldBeTrue();
        }
        finally
        {
            ResourceLimits.Memory = originalMemory;
            ResourceLimits.Width = originalWidth;
            ResourceLimits.Height = originalHeight;
            ResourceLimits.ListLength = originalListLength;
        }
    }

    [Fact]
    public async Task StartAsync_leaves_limits_untouched_when_zero()
    {
        ulong originalMemory = ResourceLimits.Memory;
        ulong originalWidth = ResourceLimits.Width;
        ulong originalHeight = ResourceLimits.Height;
        ulong originalListLength = ResourceLimits.ListLength;

        try
        {
            ImagingMagickNetOptions options = new()
            {
                MaxMemoryBytes = 0,
                MaxWidthPixels = 0,
                MaxHeightPixels = 0,
                MaxListLength = 0,
            };
            MagickNetResourceLimitsInitializer initializer = new(
                Microsoft.Extensions.Options.Options.Create(options),
                NullLogger<MagickNetResourceLimitsInitializer>.Instance);

            await initializer.StartAsync(CancellationToken.None);

            (ResourceLimits.Memory == originalMemory).ShouldBeTrue();
            (ResourceLimits.Width == originalWidth).ShouldBeTrue();
            (ResourceLimits.Height == originalHeight).ShouldBeTrue();
            (ResourceLimits.ListLength == originalListLength).ShouldBeTrue();
        }
        finally
        {
            ResourceLimits.Memory = originalMemory;
            ResourceLimits.Width = originalWidth;
            ResourceLimits.Height = originalHeight;
            ResourceLimits.ListLength = originalListLength;
        }
    }
}
