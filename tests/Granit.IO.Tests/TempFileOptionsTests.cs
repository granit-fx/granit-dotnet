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
        options.MaxLifetime.ShouldBe(TimeSpan.FromHours(1));
        options.MaxSizeBytes.ShouldBe(256L * 1024 * 1024);
        options.TenantPartition.ShouldBeTrue();
        options.JanitorInterval.ShouldBe(TimeSpan.FromMinutes(5));
        options.RunJanitor.ShouldBeTrue();
    }

    [Fact]
    public void SectionName_IsCorrect() =>
        TempFileOptions.SectionName.ShouldBe("Io:Temp");
}
