using Granit.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.Tests.Diagnostics;

public sealed class GranitActivitySourceRegistryTests
{
    [Fact]
    public void Register_adds_source_name()
    {
        string name = $"Test.Source.{Guid.NewGuid()}";

        GranitActivitySourceRegistry.Register(name);

        GranitActivitySourceRegistry.GetRegisteredSources().ShouldContain(name);
    }

    [Fact]
    public void Register_deduplicates_same_name()
    {
        string name = $"Test.Dedup.{Guid.NewGuid()}";

        GranitActivitySourceRegistry.Register(name);
        GranitActivitySourceRegistry.Register(name);

        GranitActivitySourceRegistry.GetRegisteredSources()
            .Count(s => s == name)
            .ShouldBe(1);
    }

    [Fact]
    public void Register_null_throws_ArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => GranitActivitySourceRegistry.Register(null!));

    [Fact]
    public void GetRegisteredSources_returns_snapshot()
    {
        string name = $"Test.Snapshot.{Guid.NewGuid()}";
        GranitActivitySourceRegistry.Register(name);

        IReadOnlyCollection<string> snapshot = GranitActivitySourceRegistry.GetRegisteredSources();

        snapshot.ShouldNotBeNull();
        snapshot.ShouldContain(name);
    }
}
