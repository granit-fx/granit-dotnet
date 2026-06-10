using Granit.Identity.Local.Options;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.Tests.Options;

public sealed class GranitLockoutOptionsTests
{
    [Fact]
    public void SectionName_IsCorrect() =>
        GranitLockoutOptions.SectionName.ShouldBe("Identity:Local:Lockout");

    [Fact]
    public void MaxFailedAccessAttempts_Default_IsFive() =>
        new GranitLockoutOptions().MaxFailedAccessAttempts.ShouldBe(5);

    [Fact]
    public void BaseLockoutDuration_Default_IsFiveMinutes() =>
        new GranitLockoutOptions().BaseLockoutDuration.ShouldBe(TimeSpan.FromMinutes(5));

    [Fact]
    public void MaxLockoutDuration_Default_IsTwoHours() =>
        new GranitLockoutOptions().MaxLockoutDuration.ShouldBe(TimeSpan.FromHours(2));

    [Fact]
    public void ExponentialBase_Default_IsTwo() =>
        new GranitLockoutOptions().ExponentialBase.ShouldBe(2.0);
}
