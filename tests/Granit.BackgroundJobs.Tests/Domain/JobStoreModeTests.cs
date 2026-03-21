using Granit.BackgroundJobs.Domain;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.Tests.Domain;

public sealed class JobStoreModeTests
{
    [Fact]
    public void InMemory_HasValue0() => ((int)JobStoreMode.InMemory).ShouldBe(0);

    [Fact]
    public void Durable_HasValue1() => ((int)JobStoreMode.Durable).ShouldBe(1);

    [Fact]
    public void Enum_HasExactlyTwoValues()
    {
        string[] names = Enum.GetNames<JobStoreMode>();
        names.Length.ShouldBe(2);
    }
}
