using Granit.BackgroundJobs.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.Tests.Domain;

public sealed class CronScheduleTests
{
    // =========================================================================
    // Create — valid expressions
    // =========================================================================

    [Fact]
    public void Create_FiveFieldCron_ReturnsInstance()
    {
        var schedule = CronSchedule.Create("0 * * * *");

        schedule.Value.ShouldBe("0 * * * *");
    }

    [Fact]
    public void Create_SixFieldCron_ReturnsInstance()
    {
        var schedule = CronSchedule.Create("*/30 * * * * *");

        schedule.Value.ShouldBe("*/30 * * * * *");
    }

    // =========================================================================
    // Create — validation errors
    // =========================================================================

    [Fact]
    public void Create_NullValue_ThrowsArgumentException()
    {
        Action act = () => CronSchedule.Create(null!);

        Should.Throw<ArgumentException>(act);
    }

    [Fact]
    public void Create_EmptyValue_ThrowsArgumentException()
    {
        Action act = () => CronSchedule.Create("");

        Should.Throw<ArgumentException>(act);
    }

    [Fact]
    public void Create_WhitespaceValue_ThrowsArgumentException()
    {
        Action act = () => CronSchedule.Create("   ");

        Should.Throw<ArgumentException>(act);
    }

    [Fact]
    public void Create_ExceedsMaxLength_ThrowsArgumentException()
    {
        string longCron = new('*', 101);

        Action act = () => CronSchedule.Create(longCron);

        ArgumentException ex = Should.Throw<ArgumentException>(act);
        ex.Message.ShouldContain("100");
    }

    [Theory]
    [InlineData("*")]
    [InlineData("* *")]
    [InlineData("* * *")]
    [InlineData("* * * *")]
    [InlineData("* * * * * * *")]
    public void Create_WrongFieldCount_ThrowsArgumentException(string cron)
    {
        Action act = () => CronSchedule.Create(cron);

        ArgumentException ex = Should.Throw<ArgumentException>(act);
        ex.Message.ShouldContain("5 or 6 fields");
    }

    // =========================================================================
    // Implicit conversions
    // =========================================================================

    [Fact]
    public void ImplicitConversion_ToString_ReturnsValue()
    {
        var schedule = CronSchedule.Create("0 8 * * *");

        string result = schedule;

        result.ShouldBe("0 8 * * *");
    }

    [Fact]
    public void ImplicitConversion_FromString_ReturnsCronSchedule()
    {
        CronSchedule schedule = "0 8 * * *";

        schedule.Value.ShouldBe("0 8 * * *");
    }

    // =========================================================================
    // Equality
    // =========================================================================

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        var a = CronSchedule.Create("0 * * * *");
        var b = CronSchedule.Create("0 * * * *");

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentValue_AreNotEqual()
    {
        var a = CronSchedule.Create("0 * * * *");
        var b = CronSchedule.Create("0 8 * * *");

        a.ShouldNotBe(b);
    }
}
