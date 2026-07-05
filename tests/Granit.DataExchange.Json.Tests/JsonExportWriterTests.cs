using System.Text;
using System.Text.Json;
using Granit.DataExchange.Export;
using Granit.DataExchange.Json.Internal.Export;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Json.Tests;

public sealed class JsonExportWriterTests
{
    private static readonly JsonExportWriter Sut = new();

    // ---- CanWrite --------------------------------------------------------

    [Theory]
    [InlineData("json")]
    [InlineData("JSON")]
    [InlineData("Json")]
    public void CanWrite_json_returns_true(string format) =>
        Sut.CanWrite(format).ShouldBeTrue();

    [Theory]
    [InlineData("csv")]
    [InlineData("xlsx")]
    [InlineData("xml")]
    [InlineData("")]
    public void CanWrite_other_formats_returns_false(string format) =>
        Sut.CanWrite(format).ShouldBeFalse();

    // ---- MimeType / FileExtension / Capabilities -------------------------

    [Fact]
    public void MimeType_is_application_json() =>
        Sut.MimeType.ShouldBe("application/json");

    [Fact]
    public void FileExtension_is_json() =>
        Sut.FileExtension.ShouldBe(".json");

    [Fact]
    public void Capabilities_SupportsHierarchy_is_true() =>
        Sut.Capabilities.SupportsHierarchy.ShouldBeTrue();

    // ---- WriteAsync — scalar fields -------------------------------------

    [Fact]
    public async Task WriteAsync_produces_json_array()
    {
        List<ExportFieldDescriptor> fields =
        [
            new("Name", "String", null, null, 0, false),
            new("Email", "String", null, null, 1, false),
        ];

        List<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?> { ["Name"] = "Alice", ["Email"] = "alice@test.com" },
            new Dictionary<string, object?> { ["Name"] = "Bob", ["Email"] = "bob@test.com" },
        ];

        await using var stream = new MemoryStream();
        await Sut.WriteAsync(stream, fields, ToAsyncEnumerable(rows), TestContext.Current.CancellationToken);

        stream.Position = 0;
        using var doc = JsonDocument.Parse(stream);
        doc.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().ShouldBe(2);
        doc.RootElement[0].GetProperty("Name").GetString().ShouldBe("Alice");
        doc.RootElement[0].GetProperty("Email").GetString().ShouldBe("alice@test.com");
        doc.RootElement[1].GetProperty("Name").GetString().ShouldBe("Bob");
    }

    [Fact]
    public async Task WriteAsync_empty_rows_produces_empty_array()
    {
        List<ExportFieldDescriptor> fields = [new("Name", "String", null, null, 0, false)];

        await using var stream = new MemoryStream();
        await Sut.WriteAsync(stream, fields, ToAsyncEnumerable([]), TestContext.Current.CancellationToken);

        stream.Position = 0;
        using var doc = JsonDocument.Parse(stream);
        doc.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task WriteAsync_null_values_produce_json_null()
    {
        List<ExportFieldDescriptor> fields = [new("Name", "String", null, null, 0, false)];
        List<IReadOnlyDictionary<string, object?>> rows =
            [new Dictionary<string, object?> { ["Name"] = null }];

        await using var stream = new MemoryStream();
        await Sut.WriteAsync(stream, fields, ToAsyncEnumerable(rows), TestContext.Current.CancellationToken);

        stream.Position = 0;
        using var doc = JsonDocument.Parse(stream);
        doc.RootElement[0].GetProperty("Name").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task WriteAsync_uses_PropertyPath_as_key()
    {
        // Header is for display; JSON key is always PropertyPath for round-trip
        List<ExportFieldDescriptor> fields = [new("Email", "String", "E-mail", null, 0, false)];
        List<IReadOnlyDictionary<string, object?>> rows =
            [new Dictionary<string, object?> { ["Email"] = "test@test.com" }];

        await using var stream = new MemoryStream();
        await Sut.WriteAsync(stream, fields, ToAsyncEnumerable(rows), TestContext.Current.CancellationToken);

        stream.Position = 0;
        string json = Encoding.UTF8.GetString(stream.ToArray());
        json.ShouldContain("\"Email\"");
        json.ShouldNotContain("\"E-mail\"");
    }

    // ---- WriteAsync — complex fields ------------------------------------

    [Fact]
    public async Task WriteAsync_complex_field_serializes_nested_object()
    {
        List<Tag> tags = [new("dotnet"), new("export")];

        Func<object, object?> selector = _ => tags;
        List<ExportFieldDescriptor> fields =
        [
            new("Name", "String", null, null, 0, false),
            new("Tags", "List`1", null, null, 1, false,
                RequiresHierarchy: true,
                ValueSelector: selector,
                SelectorType: typeof(List<Tag>)),
        ];

        List<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?>
            {
                ["Name"] = "Alice",
                ["Tags"] = tags,
            },
        ];

        await using var stream = new MemoryStream();
        await Sut.WriteAsync(stream, fields, ToAsyncEnumerable(rows), TestContext.Current.CancellationToken);

        stream.Position = 0;
        using var doc = JsonDocument.Parse(stream);
        JsonElement tagsEl = doc.RootElement[0].GetProperty("Tags");
        tagsEl.ValueKind.ShouldBe(JsonValueKind.Array);
        tagsEl.GetArrayLength().ShouldBe(2);
        tagsEl[0].GetProperty("Label").GetString().ShouldBe("dotnet");
    }

    [Fact]
    public async Task WriteAsync_complex_field_uses_SelectorType_not_object()
    {
        // When SelectorType is set, JSON should use that type's schema, not typeof(object)
        Animal cat = new Cat { Name = "Whiskers", Purrs = true };

        Func<object, object?> selector = _ => cat;
        List<ExportFieldDescriptor> fields =
        [
            new("Pet", "Animal", null, null, 0, false,
                RequiresHierarchy: true,
                ValueSelector: selector,
                SelectorType: typeof(Cat)),     // concrete type
        ];

        List<IReadOnlyDictionary<string, object?>> rows =
            [new Dictionary<string, object?> { ["Pet"] = cat }];

        await using var stream = new MemoryStream();
        await Sut.WriteAsync(stream, fields, ToAsyncEnumerable(rows), TestContext.Current.CancellationToken);

        stream.Position = 0;
        using var doc = JsonDocument.Parse(stream);
        JsonElement petEl = doc.RootElement[0].GetProperty("Pet");
        petEl.GetProperty("Name").GetString().ShouldBe("Whiskers");
        petEl.GetProperty("Purrs").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task WriteAsync_handles_reference_cycles_gracefully()
    {
        // IgnoreCycles should prevent JsonException on circular refs
        CycleNode node = new() { Name = "root" };
        node.Child = new CycleNode { Name = "child", Child = node }; // cycle

        Func<object, object?> selector = _ => node;
        List<ExportFieldDescriptor> fields =
        [
            new("Node", "CycleNode", null, null, 0, false,
                RequiresHierarchy: true,
                ValueSelector: selector,
                SelectorType: typeof(CycleNode)),
        ];

        List<IReadOnlyDictionary<string, object?>> rows =
            [new Dictionary<string, object?> { ["Node"] = node }];

        await using var stream = new MemoryStream();

        // Should NOT throw — IgnoreCycles breaks the cycle
        await Should.NotThrowAsync(
            () => Sut.WriteAsync(stream, fields, ToAsyncEnumerable(rows), TestContext.Current.CancellationToken));
    }

    // ---- Helpers ---------------------------------------------------------

    private static async IAsyncEnumerable<IReadOnlyDictionary<string, object?>> ToAsyncEnumerable(
        List<IReadOnlyDictionary<string, object?>> items)
    {
        foreach (IReadOnlyDictionary<string, object?> item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }

    private sealed record Tag(string Label);

    private abstract class Animal { public string Name { get; set; } = string.Empty; }

    private sealed class Cat : Animal { public bool Purrs { get; set; } }

    private sealed class CycleNode
    {
        public string Name { get; set; } = string.Empty;
        public CycleNode? Child { get; set; }
    }
}
