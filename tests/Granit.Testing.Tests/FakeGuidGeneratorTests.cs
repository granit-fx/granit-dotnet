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
    public async Task AsyncLocal_Isolates_State_Across_Tasks()
    {
        FakeGuidGenerator generator = new();
        var specificGuid = Guid.NewGuid();

#pragma warning disable xUnit1051
        var task1 = Task.Run(async () =>
        {
            generator.Enqueue(specificGuid);
            await Task.Delay(50);
            generator.Create().ShouldBe(specificGuid);
        });

        var task2 = Task.Run(async () =>
        {
            await Task.Delay(50);
            // Should NOT see specificGuid from task1
            Guid result = generator.Create();
            result.ShouldNotBe(specificGuid);
        });

        await Task.WhenAll(task1, task2);
#pragma warning restore xUnit1051
    }
}
