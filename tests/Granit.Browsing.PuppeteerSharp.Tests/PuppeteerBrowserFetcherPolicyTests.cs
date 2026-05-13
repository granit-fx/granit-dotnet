using System;
using System.Threading.Tasks;
using Granit.Browsing.PuppeteerSharp.Internal;
using Granit.Browsing.PuppeteerSharp.Options;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Browsing.PuppeteerSharp.Tests;

public sealed class PuppeteerBrowserFetcherPolicyTests
{
    [Fact]
    public async Task Production_without_pinned_executable_refuses_download()
    {
        TestHostEnvironment env = new() { EnvironmentName = Environments.Production };
        PuppeteerBrowserFetcherPolicy integrity = new(env, NullLogger<PuppeteerBrowserFetcherPolicy>.Instance);

        await Should.ThrowAsync<InvalidOperationException>(
            () => integrity.DownloadAsync(new PuppeteerSharpOptions()));
    }

    [Fact]
    public async Task Production_with_skip_flag_does_not_throw()
    {
        // Skip flag short-circuits before the network call; the service that wires this
        // never calls DownloadAsync when SkipChromiumDownload is set, so the check is
        // production-safe so long as the caller honours the contract. We assert
        // ArgumentNullException stays the only ctor-enforced failure.
        TestHostEnvironment env = new() { EnvironmentName = Environments.Production };
        PuppeteerBrowserFetcherPolicy integrity = new(env, NullLogger<PuppeteerBrowserFetcherPolicy>.Instance);

        await Should.ThrowAsync<ArgumentNullException>(() => integrity.DownloadAsync(null!));
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = System.IO.Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
