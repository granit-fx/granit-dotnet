using System.Collections.ObjectModel;
using Granit.Webhooks.Definitions;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Tests.Definitions;

public sealed class WebhookEventTypeDefinitionContextTests
{
    private readonly WebhookEventTypeDefinitionContext _context = new();

    [Fact]
    public void Add_ValidName_RegistersDefinition()
    {
        _context.Add("document.uploaded", "Document uploaded", "Fires on upload", "Documents");

        WebhookEventTypeDefinition? result = _context.GetOrNull("document.uploaded");

        result.ShouldNotBeNull();
        result.Name.ShouldBe("document.uploaded");
        result.DisplayName.ShouldBe("Document uploaded");
        result.Description.ShouldBe("Fires on upload");
        result.Category.ShouldBe("Documents");
    }

    [Fact]
    public void Add_DuplicateName_LastWins()
    {
        _context.Add("document.uploaded", "First");
        _context.Add("document.uploaded", "Second");

        WebhookEventTypeDefinition? result = _context.GetOrNull("document.uploaded");

        result.ShouldNotBeNull();
        result.DisplayName.ShouldBe("Second");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Add_NullOrWhitespaceName_Throws(string? name)
    {
        Should.Throw<ArgumentException>(() => _context.Add(name!));
    }

    [Fact]
    public void GetOrNull_UnknownName_ReturnsNull()
    {
        _context.GetOrNull("unknown.event").ShouldBeNull();
    }

    [Fact]
    public void Build_ReturnsImmutableDictionary()
    {
        _context.Add("a.event");
        _context.Add("b.event");

        ReadOnlyDictionary<string, WebhookEventTypeDefinition> built = _context.Build();

        built.Count.ShouldBe(2);
        built.ShouldContainKey("a.event");
        built.ShouldContainKey("b.event");
    }
}
