using Granit.Templating.Scriban.GlobalContexts;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Templating.Scriban.Tests.GlobalContexts;

public sealed class AppGlobalContextTests
{
    [Fact]
    public void ContextName_IsApp()
    {
        AppGlobalContext sut = new(Options.Create(new AppGlobalContextOptions()));

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
        AppGlobalContext sut = new(Options.Create(opts));

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
        AppGlobalContext sut = new(Options.Create(opts));

        dynamic resolved = sut.Resolve();

        ((string)resolved.GetType().GetProperty("base_url")!.GetValue(resolved)!)
            .ShouldBe("https://app.example.com");
    }

    [Fact]
    public void Resolve_DefaultOptions_ReturnsEmptyStrings()
    {
        AppGlobalContext sut = new(Options.Create(new AppGlobalContextOptions()));

        dynamic resolved = sut.Resolve();
        Type type = resolved.GetType();

        ((string)type.GetProperty("name")!.GetValue(resolved)!).ShouldBe(string.Empty);
        ((string)type.GetProperty("base_url")!.GetValue(resolved)!).ShouldBe(string.Empty);
        ((string)type.GetProperty("support_email")!.GetValue(resolved)!).ShouldBe(string.Empty);
        ((string)type.GetProperty("logo_url")!.GetValue(resolved)!).ShouldBe(string.Empty);
    }
}
