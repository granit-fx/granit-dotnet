using Granit.Templating.Internal;
using Granit.Templating.Keys;
using Granit.Templating.Pipeline;
using Granit.Templating.Store;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Templating.Tests.Pipeline;

public sealed class StoreTemplateResolverTests
{
    [Fact]
    public void Priority_Is100()
    {
        IDocumentTemplateStoreReader storeReader = Substitute.For<IDocumentTemplateStoreReader>();
        var resolver = new StoreTemplateResolver(storeReader);

        resolver.Priority.ShouldBe(100);
    }

    [Fact]
    public async Task TryResolveAsync_DelegatesToStoreReader()
    {
        IDocumentTemplateStoreReader storeReader = Substitute.For<IDocumentTemplateStoreReader>();
        var descriptor = new TemplateDescriptor
        {
            Content = "<p>Hello</p>",
            MimeType = "text/html",
        };
        var key = new TemplateKey("Billing.Invoice", "fr");
        storeReader.TryGetPublishedAsync(key, Arg.Any<CancellationToken>())
            .Returns(descriptor);

        var resolver = new StoreTemplateResolver(storeReader);

        TemplateDescriptor? result = await resolver.TryResolveAsync(key,
            TestContext.Current.CancellationToken);

        result.ShouldBe(descriptor);
        await storeReader.Received(1).TryGetPublishedAsync(key, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryResolveAsync_WhenStoreReturnsNull_ReturnsNull()
    {
        IDocumentTemplateStoreReader storeReader = Substitute.For<IDocumentTemplateStoreReader>();
        var key = new TemplateKey("NonExistent");
        storeReader.TryGetPublishedAsync(key, Arg.Any<CancellationToken>())
            .Returns((TemplateDescriptor?)null);

        var resolver = new StoreTemplateResolver(storeReader);

        TemplateDescriptor? result = await resolver.TryResolveAsync(key,
            TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }
}
