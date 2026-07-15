using Granit.Imaging.AI.Options;
using Shouldly;

namespace Granit.Imaging.AI.Tests;

public sealed class ImagingAIOptionsValidatorTests
{
    private readonly ImagingAIOptionsValidator _validator = new();

    [Fact]
    public void Defaults_are_valid() =>
        _validator.Validate(null, new ImagingAIOptions()).Succeeded.ShouldBeTrue();

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(301)]
    public void Unusable_timeouts_fail(int timeoutSeconds) =>
        _validator.Validate(null, new ImagingAIOptions { TimeoutSeconds = timeoutSeconds })
            .Failed.ShouldBeTrue();

    [Theory]
    [InlineData(1)]
    [InlineData(300)]
    public void Boundary_timeouts_pass(int timeoutSeconds) =>
        _validator.Validate(null, new ImagingAIOptions { TimeoutSeconds = timeoutSeconds })
            .Succeeded.ShouldBeTrue();
}
