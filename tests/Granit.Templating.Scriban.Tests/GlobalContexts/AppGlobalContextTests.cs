using Granit.Templating.Scriban.GlobalContexts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Templating.Scriban.Tests.GlobalContexts;

public sealed class AppGlobalContextTests
{
    private static IServiceScopeFactory BuildScopeFactory() =>
        new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

    [Fact]
    public void ContextName_IsApp()
    {
        IServiceScopeFactory scopeFactory = BuildScopeFactory();
        AppGlobalContext sut = new(Options.Create(new AppGlobalContextOptions()), scopeFactory);

        sut.ContextName.ShouldBe("app");
    }

    [Fact]
    public void Resolve_ReturnsConfiguredValues()
    {
        AppGlobalContextOptions opts = new()
        {
            Name = "Guava Admin",
            BaseUrl = "https://app.example.com",
            SupportEmail = "support@example.com",
            LogoUrl = "https://cdn.example.com/logo.png",
        };
        IServiceScopeFactory scopeFactory = BuildScopeFactory();
        AppGlobalContext sut = new(Options.Create(opts), scopeFactory);

        dynamic resolved = sut.Resolve();
        Type type = resolved.GetType();

        ((string)type.GetProperty("name")!.GetValue(resolved)!).ShouldBe("Guava Admin");
        ((string)type.GetProperty("base_url")!.GetValue(resolved)!).ShouldBe("https://app.example.com");
        ((string)type.GetProperty("support_email")!.GetValue(resolved)!).ShouldBe("support@example.com");
        ((string)type.GetProperty("logo_url")!.GetValue(resolved)!).ShouldBe("https://cdn.example.com/logo.png");
    }

    [Fact]
    public void Resolve_TrimsTrailingSlashFromBaseUrl()
    {
        AppGlobalContextOptions opts = new() { BaseUrl = "https://app.example.com/" };
        IServiceScopeFactory scopeFactory = BuildScopeFactory();
        AppGlobalContext sut = new(Options.Create(opts), scopeFactory);

        dynamic resolved = sut.Resolve();

        ((string)resolved.GetType().GetProperty("base_url")!.GetValue(resolved)!)
            .ShouldBe("https://app.example.com");
    }

    [Fact]
    public void Resolve_DefaultOptions_ReturnsEmptyStrings()
    {
        IServiceScopeFactory scopeFactory = BuildScopeFactory();
        AppGlobalContext sut = new(Options.Create(new AppGlobalContextOptions()), scopeFactory);

        dynamic resolved = sut.Resolve();
        Type type = resolved.GetType();

        ((string)type.GetProperty("name")!.GetValue(resolved)!).ShouldBe(string.Empty);
        ((string)type.GetProperty("base_url")!.GetValue(resolved)!).ShouldBe(string.Empty);
        ((string)type.GetProperty("support_email")!.GetValue(resolved)!).ShouldBe(string.Empty);
        ((string)type.GetProperty("logo_url")!.GetValue(resolved)!).ShouldBe(string.Empty);
    }
}
