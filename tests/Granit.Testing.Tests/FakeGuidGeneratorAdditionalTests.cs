using Granit.Testing.Fakes;
using Shouldly;

namespace Granit.Testing.Tests;

public sealed class FakeGuidGeneratorAdditionalTests
{
    [Fact]
    public void Sequential_Guids_Are_Incrementing()
    {
        FakeGuidGenerator generator = new();

        Guid first = generator.Create();
        Guid second = generator.Create();
        Guid third = generator.Create();

        // Sequential GUIDs should have incrementing first 4 bytes
        first.ShouldBe(new Guid(1, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]));
        second.ShouldBe(new Guid(2, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]));
        third.ShouldBe(new Guid(3, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]));
    }

    [Fact]
    public void Enqueue_Multiple_Returns_In_Order()
    {
        FakeGuidGenerator generator = new();
        var g1 = Guid.NewGuid();
        var g2 = Guid.NewGuid();
        var g3 = Guid.NewGuid();

        generator.Enqueue(g1);
        generator.Enqueue(g2);
        generator.Enqueue(g3);

        generator.Create().ShouldBe(g1);
        generator.Create().ShouldBe(g2);
        generator.Create().ShouldBe(g3);
    }

    [Fact]
    public void Constructor_With_No_Args_Uses_Sequential()
    {
        FakeGuidGenerator generator = new();

        Guid result = generator.Create();

        result.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Mixed_Enqueue_And_Sequential()
    {
        FakeGuidGenerator generator = new();
        var queued = Guid.NewGuid();

        generator.Enqueue(queued);
        generator.Create().ShouldBe(queued); // from queue
        Guid sequential1 = generator.Create(); // sequential (1)

        generator.Enqueue(queued);
        generator.Create().ShouldBe(queued); // from queue again
        Guid sequential2 = generator.Create(); // sequential (2)

        sequential2.ShouldNotBe(sequential1); // counter kept incrementing
    }
}
