using Granit.Localization;
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
            ctx.Add<TestResource>("document.uploaded", category: "Documents");
            ctx.Add<TestResource>("patient.created", category: "Patients");
        }));

        IReadOnlyList<WebhookEventTypeDefinition> all = registry.GetAll();

        all.Count.ShouldBe(2);
    }

    [Fact]
    public void Exists_KnownEventType_ReturnsTrue()
    {
        WebhookEventTypeRegistry registry = CreateRegistry(new TestProvider(ctx =>
            ctx.Add<TestResource>("document.uploaded")));

        registry.Exists("document.uploaded").ShouldBeTrue();
    }

    [Fact]
    public void Exists_UnknownEventType_ReturnsFalse()
    {
        WebhookEventTypeRegistry registry = CreateRegistry(new TestProvider(ctx =>
            ctx.Add<TestResource>("document.uploaded")));

        registry.Exists("hack.event").ShouldBeFalse();
    }

    [Fact]
    public void GetOrNull_KnownEventType_ReturnsDefinition()
    {
        WebhookEventTypeRegistry registry = CreateRegistry(new TestProvider(ctx =>
            ctx.Add<TestResource>("document.uploaded", category: "Documents")));

        WebhookEventTypeDefinition? result = registry.GetOrNull("document.uploaded");

        result.ShouldNotBeNull();
        result.Name.ShouldBe("document.uploaded");
        result.DisplayName!.Localize(null).ShouldBe("WebhookEventType:document.uploaded");
        result.Description!.Localize(null).ShouldBe("WebhookEventType:document.uploaded:Description");
        result.Category!.Localize(null).ShouldBe("WebhookEventTypeCategory:Documents");
    }

    [Fact]
    public void GetOrNull_UnknownEventType_ReturnsNull()
    {
        WebhookEventTypeRegistry registry = CreateRegistry(new TestProvider(ctx =>
            ctx.Add<TestResource>("document.uploaded")));

        registry.GetOrNull("unknown.event").ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Multiple providers
    // -------------------------------------------------------------------------

    [Fact]
    public void GetAll_MultipleProviders_AggregatesDefinitions()
    {
        WebhookEventTypeRegistry registry = CreateRegistry(
            new TestProvider(ctx => ctx.Add<TestResource>("document.uploaded")),
            new TestProvider(ctx => ctx.Add<TestResource>("patient.created")));

        registry.GetAll().Count.ShouldBe(2);
    }

    [Fact]
    public void GetAll_DuplicateAcrossProviders_LastWins()
    {
        WebhookEventTypeRegistry registry = CreateRegistry(
            new TestProvider(ctx => ctx.Add(new WebhookEventTypeDefinition(
                "document.uploaded", LocalizableString.Fixed("First")))),
            new TestProvider(ctx => ctx.Add(new WebhookEventTypeDefinition(
                "document.uploaded", LocalizableString.Fixed("Second")))));

        IReadOnlyList<WebhookEventTypeDefinition> all = registry.GetAll();

        all.Count.ShouldBe(1);
        all[0].DisplayName!.Localize(null).ShouldBe("Second");
    }

    // -------------------------------------------------------------------------
    // Explicit definitions
    // -------------------------------------------------------------------------

    [Fact]
    public void GetAll_ExplicitDefinitions_WorksCorrectly()
    {
        WebhookEventTypeRegistry registry = CreateRegistry(new TestProvider(ctx =>
            ctx.Add(new WebhookEventTypeDefinition(
                "order.cancelled",
                DisplayName: LocalizableString.Fixed("Order cancelled"),
                Description: LocalizableString.Fixed("Fires when order is cancelled"),
                Category: LocalizableString.Fixed("Orders")))));

        WebhookEventTypeDefinition? result = registry.GetOrNull("order.cancelled");

        result.ShouldNotBeNull();
        result.DisplayName!.Localize(null).ShouldBe("Order cancelled");
        result.Description!.Localize(null).ShouldBe("Fires when order is cancelled");
        result.Category!.Localize(null).ShouldBe("Orders");
    }

    // -------------------------------------------------------------------------
    // Pre-sorted ordering
    // -------------------------------------------------------------------------

    [Fact]
    public void GetAll_ReturnsSortedByName()
    {
        WebhookEventTypeRegistry registry = CreateRegistry(new TestProvider(ctx =>
        {
            ctx.Add<TestResource>("z.event", category: "Zebra");
            ctx.Add<TestResource>("a.event", category: "Alpha");
            ctx.Add<TestResource>("b.event", category: "Alpha");
            ctx.Add<TestResource>("c.event");
        }));

        IReadOnlyList<WebhookEventTypeDefinition> all = registry.GetAll();

        all[0].Name.ShouldBe("a.event");
        all[1].Name.ShouldBe("b.event");
        all[2].Name.ShouldBe("c.event");
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

    private sealed class TestResource;
}
