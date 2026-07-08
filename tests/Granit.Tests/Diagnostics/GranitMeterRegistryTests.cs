using Granit.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.Tests.Diagnostics;

public sealed class GranitMeterRegistryTests
{
    [Fact]
    public void Register_adds_meter_name()
    {
        string name = $"Test.Meter.{Guid.NewGuid()}";

        GranitMeterRegistry.Register(name);

        GranitMeterRegistry.GetRegisteredMeters().ShouldContain(name);
    }

    [Fact]
    public void Register_deduplicates_same_name()
    {
        string name = $"Test.Dedup.{Guid.NewGuid()}";

        GranitMeterRegistry.Register(name);
        GranitMeterRegistry.Register(name);

        GranitMeterRegistry.GetRegisteredMeters()
            .Count(m => m == name)
            .ShouldBe(1);
    }

    [Fact]
    public void Register_null_throws_ArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => GranitMeterRegistry.Register(null!));

    [Fact]
    public void GetRegisteredMeters_returns_snapshot()
    {
        string name = $"Test.Snapshot.{Guid.NewGuid()}";
        GranitMeterRegistry.Register(name);

        IReadOnlyCollection<string> snapshot = GranitMeterRegistry.GetRegisteredMeters();

        snapshot.ShouldNotBeNull();
        snapshot.ShouldContain(name);
    }
}
