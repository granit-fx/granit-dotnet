using ClosedXML.Excel;
using Granit.DocumentGeneration.Excel.Internal;
using Granit.Templating.Pipeline;
using Shouldly;
using Xunit;
// 'DocumentFormat' is a root namespace from DocumentFormat.OpenXml (transitive ClosedXML dep.).
using TemplatingDocFormat = Granit.Templating.Keys.DocumentFormat;

namespace Granit.DocumentGeneration.Excel.Tests;

public sealed class ClosedXmlTemplateEngineAdditionalTests
{
    private static readonly string ExcelMimeType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static string CreateBase64Template(Action<IXLWorksheet> configure)
    {
        using XLWorkbook wb = new();
        IXLWorksheet ws = wb.AddWorksheet("Sheet1");
        configure(ws);
        using MemoryStream ms = new();
        wb.SaveAs(ms);
        return Convert.ToBase64String(ms.ToArray());
    }

    private static ClosedXmlTemplateEngine CreateSut() => new();

    // -------------------------------------------------------------------------
    // Array substitution
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RenderAsync_ArrayProperty_ReplacesWithBracketNotation()
    {
        ClosedXmlTemplateEngine sut = CreateSut();
        string base64 = CreateBase64Template(ws =>
        {
            ws.Cell("A1").SetValue("Item: {{model.items[0].name}}");
            ws.Cell("A2").SetValue("Item: {{model.items[1].name}}");
        });
        TemplateDescriptor descriptor = new() { Content = base64, MimeType = ExcelMimeType };

        RenderedContent result = await sut.RenderAsync(
            descriptor,
            new { Items = new[] { new { Name = "Alpha" }, new { Name = "Beta" } } },
            TemplatingDocFormat.Excel,
            [],
            TestContext.Current.CancellationToken);

        BinaryRenderedContent binary = result.ShouldBeOfType<BinaryRenderedContent>();
        await using MemoryStream ms = new(binary.Bytes.ToArray());
        using XLWorkbook wb = new(ms);
        IXLWorksheet ws = wb.Worksheet(1);
        ws.Cell("A1").GetValue<string>().ShouldBe("Item: Alpha");
        ws.Cell("A2").GetValue<string>().ShouldBe("Item: Beta");
    }

    // -------------------------------------------------------------------------
    // Non-text cells are skipped
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RenderAsync_NumericCell_IsNotModified()
    {
        ClosedXmlTemplateEngine sut = CreateSut();
        string base64 = CreateBase64Template(ws =>
        {
            ws.Cell("A1").SetValue(42);
            ws.Cell("A2").SetValue("Name: {{model.name}}");
        });
        TemplateDescriptor descriptor = new() { Content = base64, MimeType = ExcelMimeType };

        RenderedContent result = await sut.RenderAsync(
            descriptor,
            new { Name = "Test" },
            TemplatingDocFormat.Excel,
            [],
            TestContext.Current.CancellationToken);

        BinaryRenderedContent binary = result.ShouldBeOfType<BinaryRenderedContent>();
        await using MemoryStream ms = new(binary.Bytes.ToArray());
        using XLWorkbook wb = new(ms);
        IXLWorksheet ws = wb.Worksheet(1);
        ws.Cell("A1").GetValue<int>().ShouldBe(42);
        ws.Cell("A2").GetValue<string>().ShouldBe("Name: Test");
    }

    // -------------------------------------------------------------------------
    // Multiple worksheets
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RenderAsync_MultipleWorksheets_ReplacesPlaceholdersInAll()
    {
        ClosedXmlTemplateEngine sut = CreateSut();

        // Create a template with two worksheets
        using XLWorkbook templateWb = new();
        IXLWorksheet ws1 = templateWb.AddWorksheet("Sheet1");
        ws1.Cell("A1").SetValue("Hello {{model.name}}");
        IXLWorksheet ws2 = templateWb.AddWorksheet("Sheet2");
        ws2.Cell("A1").SetValue("Goodbye {{model.name}}");

        await using MemoryStream templateMs = new();
        templateWb.SaveAs(templateMs);
        string base64 = Convert.ToBase64String(templateMs.ToArray());

        TemplateDescriptor descriptor = new() { Content = base64, MimeType = ExcelMimeType };

        RenderedContent result = await sut.RenderAsync(
            descriptor,
            new { Name = "World" },
            TemplatingDocFormat.Excel,
            [],
            TestContext.Current.CancellationToken);

        BinaryRenderedContent binary = result.ShouldBeOfType<BinaryRenderedContent>();
        await using MemoryStream ms = new(binary.Bytes.ToArray());
        using XLWorkbook wb = new(ms);
        wb.Worksheet("Sheet1").Cell("A1").GetValue<string>().ShouldBe("Hello World");
        wb.Worksheet("Sheet2").Cell("A1").GetValue<string>().ShouldBe("Goodbye World");
    }

    // -------------------------------------------------------------------------
    // No placeholders — passthrough
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RenderAsync_NoPlaceholders_ReturnsCellsUnchanged()
    {
        ClosedXmlTemplateEngine sut = CreateSut();
        string base64 = CreateBase64Template(ws => ws.Cell("A1").SetValue("Static text"));
        TemplateDescriptor descriptor = new() { Content = base64, MimeType = ExcelMimeType };

        RenderedContent result = await sut.RenderAsync(
            descriptor,
            new { Name = "Ignored" },
            TemplatingDocFormat.Excel,
            [],
            TestContext.Current.CancellationToken);

        BinaryRenderedContent binary = result.ShouldBeOfType<BinaryRenderedContent>();
        await using MemoryStream ms = new(binary.Bytes.ToArray());
        using XLWorkbook wb = new(ms);
        wb.Worksheet(1).Cell("A1").GetValue<string>().ShouldBe("Static text");
    }

    // -------------------------------------------------------------------------
    // CanRender with null MimeType
    // -------------------------------------------------------------------------

    [Fact]
    public void CanRender_WithNullMimeType_ReturnsFalse()
    {
        ClosedXmlTemplateEngine sut = CreateSut();
        TemplateDescriptor descriptor = new() { Content = string.Empty, MimeType = null! };

        // string.Equals handles null gracefully — should return false, not throw
        bool result = sut.CanRender(descriptor);

        result.ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // Multiple placeholders in a single cell
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RenderAsync_MultiplePlaceholdersInSameCell_ReplacesAll()
    {
        ClosedXmlTemplateEngine sut = CreateSut();
        string base64 = CreateBase64Template(ws => ws.Cell("A1").SetValue("{{model.first_name}} {{model.last_name}} ({{model.age}})"));
        TemplateDescriptor descriptor = new() { Content = base64, MimeType = ExcelMimeType };

        RenderedContent result = await sut.RenderAsync(
            descriptor,
            new { FirstName = "Jean", LastName = "Dupont", Age = 42 },
            TemplatingDocFormat.Excel,
            [],
            TestContext.Current.CancellationToken);

        BinaryRenderedContent binary = result.ShouldBeOfType<BinaryRenderedContent>();
        await using MemoryStream ms = new(binary.Bytes.ToArray());
        using XLWorkbook wb = new(ms);
        wb.Worksheet(1).Cell("A1").GetValue<string>().ShouldBe("Jean Dupont (42)");
    }

    // -------------------------------------------------------------------------
    // Boolean and null values
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RenderAsync_BooleanValue_ReplacesCorrectly()
    {
        ClosedXmlTemplateEngine sut = CreateSut();
        string base64 = CreateBase64Template(ws => ws.Cell("A1").SetValue("Active: {{model.is_active}}"));
        TemplateDescriptor descriptor = new() { Content = base64, MimeType = ExcelMimeType };

        RenderedContent result = await sut.RenderAsync(
            descriptor,
            new { IsActive = true },
            TemplatingDocFormat.Excel,
            [],
            TestContext.Current.CancellationToken);

        BinaryRenderedContent binary = result.ShouldBeOfType<BinaryRenderedContent>();
        await using MemoryStream ms = new(binary.Bytes.ToArray());
        using XLWorkbook wb = new(ms);
        string cellValue = wb.Worksheet(1).Cell("A1").GetValue<string>();
        cellValue.ShouldBe("Active: True");
    }
}
