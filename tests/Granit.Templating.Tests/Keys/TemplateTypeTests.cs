using Granit.Templating.Keys;
using Shouldly;
using Xunit;

namespace Granit.Templating.Tests.Keys;

public sealed class TemplateTypeTests
{
    private sealed record TestData(string Name, int Value);

    private sealed class TestTemplateType : TemplateType<TestData>
    {
        public override string Name => "Acme.TestTemplate";
    }

    [Fact]
    public void Name_ReturnsConcreteValue()
    {
        var sut = new TestTemplateType();

        sut.Name.ShouldBe("Acme.TestTemplate");
    }

    [Fact]
    public void DataType_ReturnsTypeOfTData()
    {
        var sut = new TestTemplateType();

        sut.DataType.ShouldBe(typeof(TestData));
    }

    [Fact]
    public void ResourceAssembly_ReturnsDeclaringAssembly()
    {
        var sut = new TestTemplateType();

        sut.ResourceAssembly.ShouldBe(typeof(TestTemplateType).Assembly);
    }

    [Fact]
    public void ResourceAssembly_IsNotSourceAssembly()
    {
        var sut = new TestTemplateType();

        // The test subclass lives in the test assembly, not in Granit.Templating
        sut.ResourceAssembly.ShouldNotBe(typeof(TemplateType<>).Assembly);
    }

    private sealed class AnotherDataModel;

    private sealed class AnotherTemplateType : TemplateType<AnotherDataModel>
    {
        public override string Name => "Acme.Another";
    }

    [Fact]
    public void DataType_DifferentGenericArg_ReturnsDifferentType()
    {
        var sut = new AnotherTemplateType();

        sut.DataType.ShouldBe(typeof(AnotherDataModel));
        sut.DataType.ShouldNotBe(typeof(TestData));
    }
}
