using System.Threading.Tasks;
using Granit.Browsing.PuppeteerSharp.Internal;
using NSubstitute;
using PuppeteerSharp;
using Xunit;

namespace Granit.Browsing.PuppeteerSharp.Tests;

public sealed class PuppeteerJsContextGuardTests
{
    [Fact]
    public async Task Does_not_disable_when_neither_sandbox_nor_options_require_it()
    {
        IPage page = Substitute.For<IPage>();
        await PuppeteerJsContextGuard.ApplyAsync(page, pageJsEnabled: true, sandboxDisablesJs: false);
        await page.DidNotReceive().SetJavaScriptEnabledAsync(Arg.Any<bool>());
    }

    [Fact]
    public async Task Disables_when_sandbox_requires_it()
    {
        IPage page = Substitute.For<IPage>();
        await PuppeteerJsContextGuard.ApplyAsync(page, pageJsEnabled: true, sandboxDisablesJs: true);
        await page.Received(1).SetJavaScriptEnabledAsync(false);
    }

    [Fact]
    public async Task Disables_when_page_options_require_it()
    {
        IPage page = Substitute.For<IPage>();
        await PuppeteerJsContextGuard.ApplyAsync(page, pageJsEnabled: false, sandboxDisablesJs: false);
        await page.Received(1).SetJavaScriptEnabledAsync(false);
    }
}
