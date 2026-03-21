using Granit.Privacy.Options;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Tests.Options;

public sealed class GranitPrivacyOptionsTests
{
    [Fact]
    public void SectionName_IsPrivacy() => GranitPrivacyOptions.SectionName.ShouldBe("Privacy");

    [Fact]
    public void ExportTimeoutMinutes_DefaultValue_IsFive()
    {
        GranitPrivacyOptions options = new();

        options.ExportTimeoutMinutes.ShouldBe(5);
    }

    [Fact]
    public void ExportMaxSizeMb_DefaultValue_IsOneHundred()
    {
        GranitPrivacyOptions options = new();

        options.ExportMaxSizeMb.ShouldBe(100);
    }

    [Fact]
    public void ExportTimeoutMinutes_CanBeSet()
    {
        GranitPrivacyOptions options = new() { ExportTimeoutMinutes = 30 };

        options.ExportTimeoutMinutes.ShouldBe(30);
    }

    [Fact]
    public void ExportMaxSizeMb_CanBeSet()
    {
        GranitPrivacyOptions options = new() { ExportMaxSizeMb = 500 };

        options.ExportMaxSizeMb.ShouldBe(500);
    }
}
