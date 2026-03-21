using Granit.Webhooks.Definitions;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Tests.Definitions;

public sealed class WebhookEventTypeRegistryTests
{
    // -------------------------------------------------------------------------
    // Empty registry
    // -------------------------------------------------------------------------

    [Fact]
    public void GetAll_NoProviders_ReturnsEmptyList()
    {
        WebhookEventTypeRegistry registry = CreateRegistry();

        registry.GetAll().ShouldBeEmpty();
    }

    [Fact]
    public void Exists_NoProviders_ReturnsFalse()
    {
        WebhookEventTypeRegistry registry = CreateRegistry();

        registry.Exists("anything").ShouldBeFalse();
    }

    [Fact]
    public void GetOrNull_NoProviders_ReturnsNull()
    {
        WebhookEventTypeRegistry registry = CreateRegistry();

        registry.GetOrNull("anything").ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Single provider
    // -------------------------------------------------------------------------

    [Fact]
    public void GetAll_SingleProvider_ReturnsDefinitions()
    {
        WebhookEventTypeRegistry registry = CreateRegistry(new TestProvider(ctx =>
        {
            ctx.Add("document.uploaded", "Document uploaded", category: "Documents");
            ctx.Add("patient.created", "Patient created", category: "Patients");
        }));

        IReadOnlyList<WebhookEventTypeDefinition> all = registry.GetAll();

        all.Count.ShouldBe(2);
    }

    [Fact]
    public void Exists_KnownEventType_ReturnsTrue()
    {
        WebhookEventTypeRegistry registry = CreateRegistry(new TestProvider(ctx =>
            ctx.Add("document.uploaded")));

        registry.Exists("document.uploaded").ShouldBeTrue();
    }

    [Fact]
    public void Exists_UnknownEventType_ReturnsFalse()
    {
        WebhookEventTypeRegistry registry = CreateRegistry(new TestProvider(ctx =>
            ctx.Add("document.uploaded")));

        registry.Exists("hack.event").ShouldBeFalse();
    }

    [Fact]
    public void GetOrNull_KnownEventType_ReturnsDefinition()
    {
        WebhookEventTypeRegistry registry = CreateRegistry(new TestProvider(ctx =>
            ctx.Add("document.uploaded", "Doc uploaded", "Fires on upload", "Documents")));

        WebhookEventTypeDefinition? result = registry.GetOrNull("document.uploaded");

        result.ShouldNotBeNull();
        result.Name.ShouldBe("document.uploaded");
        result.DisplayName.ShouldBe("Doc uploaded");
        result.Description.ShouldBe("Fires on upload");
        result.Category.ShouldBe("Documents");
    }

    [Fact]
    public void GetOrNull_UnknownEventType_ReturnsNull()
    {
        WebhookEventTypeRegistry registry = CreateRegistry(new TestProvider(ctx =>
            ctx.Add("document.uploaded")));

        registry.GetOrNull("unknown.event").ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Multiple providers
    // -------------------------------------------------------------------------

    [Fact]
    public void GetAll_MultipleProviders_AggregatesDefinitions()
    {
        WebhookEventTypeRegistry registry = CreateRegistry(
            new TestProvider(ctx => ctx.Add("document.uploaded")),
            new TestProvider(ctx => ctx.Add("patient.created")));

        registry.GetAll().Count.ShouldBe(2);
    }

    [Fact]
    public void GetAll_DuplicateAcrossProviders_LastWins()
    {
        WebhookEventTypeRegistry registry = CreateRegistry(
            new TestProvider(ctx => ctx.Add("document.uploaded", "First")),
            new TestProvider(ctx => ctx.Add("document.uploaded", "Second")));

        IReadOnlyList<WebhookEventTypeDefinition> all = registry.GetAll();

        all.Count.ShouldBe(1);
        all[0].DisplayName.ShouldBe("Second");
    }

    // -------------------------------------------------------------------------
    // Pre-sorted ordering
    // -------------------------------------------------------------------------

    [Fact]
    public void GetAll_ReturnsSortedByCategoryThenName()
    {
        WebhookEventTypeRegistry registry = CreateRegistry(new TestProvider(ctx =>
        {
            ctx.Add("z.event", category: "Zebra");
            ctx.Add("a.event", category: "Alpha");
            ctx.Add("b.event", category: "Alpha");
            ctx.Add("c.event");
        }));

        IReadOnlyList<WebhookEventTypeDefinition> all = registry.GetAll();

        // null category sorts before "Alpha" (StringComparer.OrdinalIgnoreCase)
        all[0].Name.ShouldBe("c.event");
        all[1].Name.ShouldBe("a.event");
        all[2].Name.ShouldBe("b.event");
        all[3].Name.ShouldBe("z.event");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static WebhookEventTypeRegistry CreateRegistry(
        params IWebhookEventTypeDefinitionProvider[] providers) =>
        new(providers);

    private sealed class TestProvider(
        Action<IWebhookEventTypeDefinitionContext> configure) : IWebhookEventTypeDefinitionProvider
    {
        public void Define(IWebhookEventTypeDefinitionContext context) => configure(context);
    }
}
