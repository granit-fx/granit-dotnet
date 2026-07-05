using System.Xml.Linq;
using Granit.DataExchange.Export;
using Granit.DataExchange.Xml.Internal.Export;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Xml.Tests;

public sealed class XmlExportWriterTests
{
    private static readonly XmlExportWriter Sut = new();

    // ---- CanWrite --------------------------------------------------------

    [Theory]
    [InlineData("xml")]
    [InlineData("XML")]
    [InlineData("Xml")]
    public void CanWrite_xml_returns_true(string format) =>
        Sut.CanWrite(format).ShouldBeTrue();

    [Theory]
    [InlineData("csv")]
    [InlineData("xlsx")]
    [InlineData("json")]
    [InlineData("")]
    public void CanWrite_other_formats_returns_false(string format) =>
        Sut.CanWrite(format).ShouldBeFalse();

    // ---- MimeType / FileExtension / Capabilities -------------------------

    [Fact]
    public void MimeType_is_application_xml() =>
        Sut.MimeType.ShouldBe("application/xml");

    [Fact]
    public void FileExtension_is_xml() =>
        Sut.FileExtension.ShouldBe(".xml");

    [Fact]
    public void Capabilities_SupportsHierarchy_is_true() =>
        Sut.Capabilities.SupportsHierarchy.ShouldBeTrue();

    // ---- WriteAsync — scalar fields -------------------------------------

    [Fact]
    public async Task WriteAsync_produces_export_root_with_row_elements()
    {
        List<ExportFieldDescriptor> fields =
        [
            new("Name", "String", null, null, 0, false),
            new("Email", "String", null, null, 1, false),
        ];

        List<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?> { ["Name"] = "Alice", ["Email"] = "alice@test.com" },
        ];

        XDocument doc = await WriteAndParseAsync(fields, rows);

        doc.Root!.Name.LocalName.ShouldBe("Export");
        XElement row = doc.Root.Elements("Row").Single();
        row.Element("Name")!.Value.ShouldBe("Alice");
        row.Element("Email")!.Value.ShouldBe("alice@test.com");
    }

    [Fact]
    public async Task WriteAsync_empty_rows_produces_empty_export_element()
    {
        List<ExportFieldDescriptor> fields = [new("Name", "String", null, null, 0, false)];

        XDocument doc = await WriteAndParseAsync(fields, []);

        doc.Root!.Name.LocalName.ShouldBe("Export");
        doc.Root.Elements("Row").ShouldBeEmpty();
    }

    [Fact]
    public async Task WriteAsync_null_values_produce_empty_element()
    {
        List<ExportFieldDescriptor> fields = [new("Name", "String", null, null, 0, false)];
        List<IReadOnlyDictionary<string, object?>> rows =
            [new Dictionary<string, object?> { ["Name"] = null }];

        XDocument doc = await WriteAndParseAsync(fields, rows);

        XElement nameEl = doc.Root!.Element("Row")!.Element("Name")!;
        nameEl.Value.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task WriteAsync_converts_nav_field_dots_to_underscores()
    {
        List<ExportFieldDescriptor> fields = [new("Company.Name", "String", null, null, 0, true)];
        List<IReadOnlyDictionary<string, object?>> rows =
            [new Dictionary<string, object?> { ["Company.Name"] = "Acme" }];

        XDocument doc = await WriteAndParseAsync(fields, rows);

        // "Company.Name" → "Company_Name" as XML element
        doc.Root!.Element("Row")!.Element("Company_Name")!.Value.ShouldBe("Acme");
    }

    // ---- WriteAsync — complex fields ------------------------------------

    [Fact]
    public async Task WriteAsync_complex_field_serializes_sub_elements()
    {
        ProductCategory category = new() { Id = 42, Label = "Electronics" };

        Func<object, object?> selector = _ => category;
        List<ExportFieldDescriptor> fields =
        [
            new("Name", "String", null, null, 0, false),
            new("Category", "ProductCategory", null, null, 1, false,
                RequiresHierarchy: true,
                ValueSelector: selector,
                SelectorType: typeof(ProductCategory)),
        ];

        List<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?>
            {
                ["Name"] = "Laptop",
                ["Category"] = category,
            },
        ];

        XDocument doc = await WriteAndParseAsync(fields, rows);
        XElement categoryEl = doc.Root!.Element("Row")!.Element("Category")!;
        categoryEl.ShouldNotBeNull();
        // XmlSerializer wraps the type in its own element — the root wrapper is stripped
        // so the inner content (Id, Label) should appear as children
        string content = categoryEl.ToString();
        content.ShouldContain("42");
        content.ShouldContain("Electronics");
    }

    [Fact]
    public async Task WriteAsync_interface_type_throws_InvalidOperationException()
    {
        Func<object, object?> selector = _ => new List<string> { "a" };
        List<ExportFieldDescriptor> fields =
        [
            new("Items", "IList`1", null, null, 0, false,
                RequiresHierarchy: true,
                ValueSelector: selector,
                SelectorType: typeof(IList<string>)),    // interface → XmlSerializer cannot handle this
        ];

        List<IReadOnlyDictionary<string, object?>> rows =
            [new Dictionary<string, object?> { ["Items"] = new List<string> { "a" } }];

        await Should.ThrowAsync<InvalidOperationException>(
            () => Sut.WriteAsync(new MemoryStream(), fields, ToAsyncEnumerable(rows), TestContext.Current.CancellationToken));
    }

    // ---- Helpers ---------------------------------------------------------

    private static async Task<XDocument> WriteAndParseAsync(
        List<ExportFieldDescriptor> fields,
        List<IReadOnlyDictionary<string, object?>> rows)
    {
        await using MemoryStream stream = new();
        await Sut.WriteAsync(stream, fields, ToAsyncEnumerable(rows), TestContext.Current.CancellationToken);
        stream.Position = 0;
        return XDocument.Load(stream);
    }

    private static async IAsyncEnumerable<IReadOnlyDictionary<string, object?>> ToAsyncEnumerable(
        List<IReadOnlyDictionary<string, object?>> items)
    {
        foreach (IReadOnlyDictionary<string, object?> item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }

    public sealed class ProductCategory
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
    }
}
