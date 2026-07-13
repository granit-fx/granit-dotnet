using Granit.Templating.Scriban.GlobalContexts;
using Granit.Timing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Templating.Scriban.Tests.GlobalContexts;

public sealed class NowGlobalContextTests
{
    [Fact]
    public void ContextName_IsNow()
    {
        IClock clock = Substitute.For<IClock>();
        NowGlobalContext sut = new(clock);

        sut.ContextName.ShouldBe("now");
    }

    [Fact]
    public async Task Resolve_ReturnsExpectedDateFormats()
    {
        DateTimeOffset fixedTime = new(2026, 2, 27, 14, 35, 0, TimeSpan.Zero);

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(fixedTime);

        NowGlobalContext sut = new(clock);
        dynamic resolved = await sut.ResolveAsync(TestContext.Current.CancellationToken);

        // Verify properties via reflection (anonymous type)
        System.Type type = resolved.GetType();

        ((string)type.GetProperty("date")!.GetValue(resolved)!).ShouldBe("27/02/2026");
        ((string)type.GetProperty("datetime")!.GetValue(resolved)!).ShouldBe("27/02/2026 14:35");
        ((string)type.GetProperty("year")!.GetValue(resolved)!).ShouldBe("2026");
        ((string)type.GetProperty("month")!.GetValue(resolved)!).ShouldBe("02");
        ((string)type.GetProperty("day")!.GetValue(resolved)!).ShouldBe("27");
        ((string)type.GetProperty("time")!.GetValue(resolved)!).ShouldBe("14:35");
    }

    [Fact]
    public async Task Resolve_IsoFormat_IsRoundTrippable()
    {
        DateTimeOffset fixedTime = new(2026, 2, 27, 14, 35, 0, TimeSpan.FromHours(1));

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(fixedTime);

        NowGlobalContext sut = new(clock);
        dynamic resolved = await sut.ResolveAsync(TestContext.Current.CancellationToken);

        string iso = (string)resolved.GetType().GetProperty("iso")!.GetValue(resolved)!;
        DateTimeOffset.TryParse(iso, out DateTimeOffset parsed).ShouldBeTrue();
        parsed.ShouldBe(fixedTime);
    }
}
