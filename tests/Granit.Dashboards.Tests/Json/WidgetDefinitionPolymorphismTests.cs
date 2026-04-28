using System.Text.Json;
using Granit.Dashboards.Json;
using Granit.Dashboards.Widgets;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Tests.Json;

/// <summary>
/// Locks the wire format for <see cref="WidgetDefinition"/> polymorphism — the discriminator
/// property is <c>"type"</c>, the three abstractions widgets carry stable short tags
/// (<c>"markdown"</c>, <c>"image"</c>, <c>"text"</c>), and downstream packages can extend the
/// chain at runtime via <see cref="WidgetDefinitionPolymorphism.AddDerivedType{TWidget}"/>.
/// </summary>
public sealed class WidgetDefinitionPolymorphismTests
{
    [Fact]
    public void Markdown_SerializesWithStableDiscriminator()
    {
        WidgetDefinition widget = new MarkdownWidgetDefinition(
            "Banner", "Widget:Sample.Banner", Position: 0);

        string json = JsonSerializer.Serialize(widget);

        json.ShouldContain("\"type\":\"markdown\"");
        json.ShouldNotContain("$type"); // legacy CLR-typed shape banished
    }

    [Fact]
    public void Image_SerializesWithStableDiscriminator()
    {
        WidgetDefinition widget = new ImageWidgetDefinition(
            "Logo", "https://cdn.example/logo.png", "Widget:Sample.Logo.Alt", Position: 0);

        string json = JsonSerializer.Serialize(widget);

        json.ShouldContain("\"type\":\"image\"");
    }

    [Fact]
    public void Text_SerializesWithStableDiscriminator()
    {
        WidgetDefinition widget = new TextWidgetDefinition(
            "Title", "Widget:Sample.Title", TextStyle.Heading, Position: 0);

        string json = JsonSerializer.Serialize(widget);

        json.ShouldContain("\"type\":\"text\"");
    }

    [Fact]
    public void RoundTrip_PreservesConcreteType_ForAllAbstractionsWidgets()
    {
        WidgetDefinition[] originals =
        [
            new MarkdownWidgetDefinition("M", "Widget:M", Position: 0),
            new ImageWidgetDefinition("I", "https://x", "Widget:I.Alt", Position: 1),
            new TextWidgetDefinition("T", "Widget:T", TextStyle.Body, Position: 2),
        ];

        foreach (WidgetDefinition original in originals)
        {
            string json = JsonSerializer.Serialize(original);
            WidgetDefinition? decoded = JsonSerializer.Deserialize<WidgetDefinition>(json);

            decoded.ShouldNotBeNull();
            decoded.GetType().ShouldBe(original.GetType());
            decoded.Slug.ShouldBe(original.Slug);
        }
    }

    [Fact]
    public void RuntimeDerivedType_IsRoundTrippable()
    {
        // Simulates the Granit.Analytics registration path: a downstream package
        // extends the chain on a fresh JsonSerializerOptions.
        JsonSerializerOptions options = new();
        options.AddDerivedType<RuntimeRegisteredWidget>("test-widget");

        WidgetDefinition widget = new RuntimeRegisteredWidget("Custom", Position: 0);
        string json = JsonSerializer.Serialize(widget, options);
        WidgetDefinition? decoded = JsonSerializer.Deserialize<WidgetDefinition>(json, options);

        json.ShouldContain("\"type\":\"test-widget\"");
        decoded.ShouldBeOfType<RuntimeRegisteredWidget>();
    }

    [Fact]
    public void AddDerivedType_RejectsNullOptions()
    {
        Should.Throw<ArgumentNullException>(() =>
            ((JsonSerializerOptions)null!).AddDerivedType<RuntimeRegisteredWidget>("x"));
    }

    [Fact]
    public void AddDerivedType_RejectsEmptyDiscriminator()
    {
        JsonSerializerOptions options = new();
        Should.Throw<ArgumentException>(() => options.AddDerivedType<RuntimeRegisteredWidget>(string.Empty));
    }

    private sealed record RuntimeRegisteredWidget(string Slug, int Position)
        : WidgetDefinition(Slug, Position, WidgetSize.SmallKpi, RequiredPermission: null);
}
