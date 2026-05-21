using Granit.IO.Options;
using Shouldly;
using Xunit;

namespace Granit.IO.Tests;

public sealed class TempFileOptionsTests
{
    [Fact]
    public void Defaults_Match_Specification()
    {
        TempFileOptions options = new();

        options.RootDirectory.ShouldBeNull();
        options.ChmodOwnerOnly.ShouldBeTrue();
        options.MaxLifetime.ShouldBe(TimeSpan.FromMinutes(30));
        options.MaxSizeBytes.ShouldBe(100L * 1024 * 1024);
        options.TenantPartition.ShouldBeTrue();
        options.JanitorInterval.ShouldBe(TimeSpan.FromMinutes(5));
        options.RunJanitor.ShouldBeTrue();
    }

    [Fact]
    public void SectionName_IsCorrect() =>
        TempFileOptions.SectionName.ShouldBe("IO:TempFiles");

    [Fact]
    public void EffectiveRootDirectory_FallsBackToDefault_WhenUnset()
    {
        TempFileOptions options = new();
        options.EffectiveRootDirectory.ShouldBe(TempFileOptions.DefaultRootDirectory);
    }

    [Fact]
    public void EffectiveRootDirectory_HonorsExplicitValue()
    {
        TempFileOptions options = new() { RootDirectory = "/var/tmp/x" };
        options.EffectiveRootDirectory.ShouldBe("/var/tmp/x");
    }
}
