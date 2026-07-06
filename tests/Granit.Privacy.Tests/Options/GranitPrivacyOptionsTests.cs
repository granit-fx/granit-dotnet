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

    // ── Deletion cooling-off ─────────────────────────────────────────────────

    [Fact]
    public void DefaultGracePeriodDays_DefaultValue_IsThirty()
    {
        GranitPrivacyOptions options = new();

        options.DefaultGracePeriodDays.ShouldBe(30);
    }

    [Fact]
    public void MaxGracePeriodDays_DefaultValue_IsNinety()
    {
        GranitPrivacyOptions options = new();

        options.MaxGracePeriodDays.ShouldBe(90);
    }

    [Fact]
    public void ReminderDaysBefore_DefaultValue_IsThree()
    {
        GranitPrivacyOptions options = new();

        options.ReminderDaysBefore.ShouldBe(3);
    }
}
