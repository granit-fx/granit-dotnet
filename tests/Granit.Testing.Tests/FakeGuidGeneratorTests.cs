using Granit.Testing.Fakes;
using Shouldly;

namespace Granit.Testing.Tests;

public sealed class FakeGuidGeneratorTests
{
    [Fact]
    public void Create_Returns_Sequential_Guids_When_Queue_Empty()
    {
        FakeGuidGenerator generator = new();

        Guid first = generator.Create();
        Guid second = generator.Create();

        first.ShouldNotBe(Guid.Empty);
        second.ShouldNotBe(Guid.Empty);
        first.ShouldNotBe(second);
    }

    [Fact]
    public void Create_Dequeues_When_Queue_Has_Values()
    {
        var expected = Guid.NewGuid();
        FakeGuidGenerator generator = new();
        generator.Enqueue(expected);

        Guid result = generator.Create();

        result.ShouldBe(expected);
    }

    [Fact]
    public void Create_Falls_Back_To_Sequential_After_Queue_Exhausted()
    {
        var queued = Guid.NewGuid();
        FakeGuidGenerator generator = new();
        generator.Enqueue(queued);

        Guid first = generator.Create(); // dequeue
        Guid second = generator.Create(); // sequential

        first.ShouldBe(queued);
        second.ShouldNotBe(queued);
        second.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Constructor_With_Guids_Pre_Enqueues()
    {
        var g1 = Guid.NewGuid();
        var g2 = Guid.NewGuid();
        FakeGuidGenerator generator = new(g1, g2);

        generator.Create().ShouldBe(g1);
        generator.Create().ShouldBe(g2);
    }

    [Fact]
    public void Sequential_Guids_Are_Deterministic()
    {
        FakeGuidGenerator gen1 = new();
        FakeGuidGenerator gen2 = new();

        Guid a = gen1.Create();
        Guid b = gen2.Create();

        a.ShouldBe(b);
    }

    [Fact]
    public async Task Counter_PersistsAcross_Awaits()
    {
        // AsyncLocal resets to 0 on each continuation — that was the bug.
        // Interlocked.Increment advances the counter monotonically regardless of the
        // synchronization context, so sequential calls always produce distinct GUIDs.
        FakeGuidGenerator generator = new();

        Guid first = generator.Create();
        await Task.Delay(1, TestContext.Current.CancellationToken);
        Guid second = generator.Create();
        await Task.Delay(1, TestContext.Current.CancellationToken);
        Guid third = generator.Create();

        first.ShouldNotBe(second, "counter must advance after await");
        second.ShouldNotBe(third, "counter must continue advancing across awaits");
        first.ShouldNotBe(third);
    }

    [Fact]
    public void Separate_Instances_Are_Independent()
    {
        // Isolation is guaranteed by each test creating its own instance, not by AsyncLocal.
        FakeGuidGenerator gen1 = new();
        FakeGuidGenerator gen2 = new();

        gen1.Create(); // advance gen1 counter to 1
        gen1.Create(); // advance gen1 counter to 2

        Guid fromGen2 = gen2.Create(); // gen2 counter starts at 0 independently → 1

        fromGen2.ShouldBe(new Guid(1, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]),
            "gen2 counter is independent of gen1");
    }
}
