using System.Collections.ObjectModel;
using Granit.Core.Localization;
using Granit.Webhooks.Definitions;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Tests.Definitions;

public sealed class WebhookEventTypeDefinitionContextTests
{
    private readonly WebhookEventTypeDefinitionContext _context = new();

    // -------------------------------------------------------------------------
    // Add<TResource> (convention-based)
    // -------------------------------------------------------------------------

    [Fact]
    public void AddGeneric_ValidName_RegistersWithConventionKeys()
    {
        _context.Add<TestLocalizationResource>("document.uploaded", category: "Documents");

        WebhookEventTypeDefinition? result = _context.GetOrNull("document.uploaded");

        result.ShouldNotBeNull();
        result.Name.ShouldBe("document.uploaded");
        result.DisplayName!.Localize(null).ShouldBe("WebhookEventType:document.uploaded");
        result.Description!.Localize(null).ShouldBe("WebhookEventType:document.uploaded:Description");
        result.Category!.Localize(null).ShouldBe("WebhookEventTypeCategory:Documents");
    }

    [Fact]
    public void AddGeneric_NullCategory_SetsNullCategory()
    {
        _context.Add<TestLocalizationResource>("document.uploaded");

        WebhookEventTypeDefinition? result = _context.GetOrNull("document.uploaded");

        result.ShouldNotBeNull();
        result.Category.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddGeneric_NullOrWhitespaceName_Throws(string? name) => Should.Throw<ArgumentException>(() => _context.Add<TestLocalizationResource>(name!));

    // -------------------------------------------------------------------------
    // Add(WebhookEventTypeDefinition) (explicit)
    // -------------------------------------------------------------------------

    [Fact]
    public void AddExplicit_ValidDefinition_RegistersDefinition()
    {
        var definition = new WebhookEventTypeDefinition(
            "patient.created",
            DisplayName: LocalizableString.Fixed("Patient created"),
            Category: LocalizableString.Fixed("Patients"));

        _context.Add(definition);

        WebhookEventTypeDefinition? result = _context.GetOrNull("patient.created");

        result.ShouldNotBeNull();
        result.DisplayName!.Localize(null).ShouldBe("Patient created");
        result.Category!.Localize(null).ShouldBe("Patients");
    }

    [Fact]
    public void AddExplicit_NullDefinition_Throws() => Should.Throw<ArgumentNullException>(() => _context.Add(null!));

    [Fact]
    public void AddExplicit_WhitespaceName_Throws()
    {
        var definition = new WebhookEventTypeDefinition("   ");

        Should.Throw<ArgumentException>(() => _context.Add(definition));
    }

    // -------------------------------------------------------------------------
    // Duplicate handling
    // -------------------------------------------------------------------------

    [Fact]
    public void Add_DuplicateName_LastWins()
    {
        _context.Add<TestLocalizationResource>("document.uploaded", category: "First");
        _context.Add(new WebhookEventTypeDefinition(
            "document.uploaded",
            DisplayName: LocalizableString.Fixed("Explicit")));

        WebhookEventTypeDefinition? result = _context.GetOrNull("document.uploaded");

        result.ShouldNotBeNull();
        result.DisplayName!.Localize(null).ShouldBe("Explicit");
        result.Category.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // GetOrNull / Build
    // -------------------------------------------------------------------------

    [Fact]
    public void GetOrNull_UnknownName_ReturnsNull() => _context.GetOrNull("unknown.event").ShouldBeNull();

    [Fact]
    public void Build_ReturnsImmutableDictionary()
    {
        _context.Add<TestLocalizationResource>("a.event");
        _context.Add<TestLocalizationResource>("b.event");

        ReadOnlyDictionary<string, WebhookEventTypeDefinition> built = _context.Build();

        built.Count.ShouldBe(2);
        built.ShouldContainKey("a.event");
        built.ShouldContainKey("b.event");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private sealed class TestLocalizationResource;
}
