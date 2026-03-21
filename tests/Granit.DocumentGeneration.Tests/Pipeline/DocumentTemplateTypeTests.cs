using Granit.DocumentGeneration.Pipeline;
using Granit.Templating.Keys;
using Shouldly;
using Xunit;

namespace Granit.DocumentGeneration.Tests.Pipeline;

public sealed class DocumentTemplateTypeTests
{
    [Fact]
    public void DefaultFormat_IsPdf()
    {
        TestTemplateType templateType = new();

        templateType.DefaultFormat.ShouldBe(DocumentFormat.Pdf);
    }

    [Fact]
    public void DefaultFormat_CanBeOverridden()
    {
        ExcelTemplateType templateType = new();

        templateType.DefaultFormat.ShouldBe(DocumentFormat.Excel);
    }

    [Fact]
    public void Name_IsDefinedBySubclass()
    {
        TestTemplateType templateType = new();

        templateType.Name.ShouldBe("Test.Document");
    }

    private sealed class TestTemplateType : DocumentTemplateType<TestData>
    {
        public override string Name => "Test.Document";
    }

    private sealed class ExcelTemplateType : DocumentTemplateType<TestData>
    {
        public override string Name => "Test.Excel";
        public override DocumentFormat DefaultFormat => DocumentFormat.Excel;
    }

    private sealed record TestData(string Value);
}
