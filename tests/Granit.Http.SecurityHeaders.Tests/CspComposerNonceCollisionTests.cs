using Granit.Http.SecurityHeaders.Contributors;
using Granit.Http.SecurityHeaders.Internal;
using Granit.Http.SecurityHeaders.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.SecurityHeaders.Tests;

/// <summary>
/// Verifies the runtime warning when a directive ends up with both
/// <c>'unsafe-inline'</c> and a nonce/hash source — the nonce/hash silently
/// suppresses <c>'unsafe-inline'</c> in browsers, so the contributor's
/// relaxation would be dropped without anyone noticing.
/// </summary>
public sealed class CspComposerNonceCollisionTests
{
    [Fact]
    public void Compose_WhenNonceAndUnsafeInlineCoexist_LogsWarning()
    {
        StubContributor c = new(b => b
            .AddScriptSrc("'self'", "'unsafe-inline'", "'nonce-abc=='"));

        Internal.RecordingLogger<CspComposer> logger = new();
        CspComposer composer = BuildComposer(c, logger);
        HttpContext ctx = MakeContextWithMatchedEndpoint();

        _ = composer.Compose(ctx);

        logger.Entries.ShouldContain(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("nonce/hash") && e.Message.Contains("'unsafe-inline'"));
    }

    [Fact]
    public void Compose_WhenOnlyNonce_NoWarning()
    {
        StubContributor c = new(b => b.AddScriptSrc("'self'", "'nonce-abc=='"));
        Internal.RecordingLogger<CspComposer> logger = new();
        CspComposer composer = BuildComposer(c, logger);
        HttpContext ctx = MakeContextWithMatchedEndpoint();

        _ = composer.Compose(ctx);

        logger.Entries.ShouldNotContain(e => e.Message.Contains("nonce/hash"));
    }

    [Fact]
    public void Compose_WhenOnlyUnsafeInline_NoCollisionWarning()
    {
        StubContributor c = new(b => b.AddScriptSrc("'self'", "'unsafe-inline'"));
        Internal.RecordingLogger<CspComposer> logger = new();
        CspComposer composer = BuildComposer(c, logger);
        HttpContext ctx = MakeContextWithMatchedEndpoint();

        _ = composer.Compose(ctx);

        logger.Entries.ShouldNotContain(e => e.Message.Contains("nonce/hash"));
    }

    [Fact]
    public void Compose_WhenSha256AndUnsafeInline_LogsWarning()
    {
        StubContributor c = new(b => b
            .AddScriptSrc("'unsafe-inline'", "'sha256-AbCdEf=='"));

        Internal.RecordingLogger<CspComposer> logger = new();
        CspComposer composer = BuildComposer(c, logger);
        HttpContext ctx = MakeContextWithMatchedEndpoint();

        _ = composer.Compose(ctx);

        logger.Entries.ShouldContain(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("nonce/hash"));
    }

    [Fact]
    public void Compose_SameEndpoint_LogsNonceCollisionOncePerEndpoint()
    {
        StubContributor c = new(b => b.AddScriptSrc("'unsafe-inline'", "'nonce-abc=='"));
        Internal.RecordingLogger<CspComposer> logger = new();
        CspComposer composer = BuildComposer(c, logger);
        HttpContext ctx = MakeContextWithMatchedEndpoint();

        // First request → cache miss → warning fires.
        _ = composer.Compose(ctx);
        int firstCount = logger.Entries.Count(e => e.Message.Contains("nonce/hash"));

        // Second request on same endpoint → cache hit → no extra warning.
        _ = composer.Compose(ctx);
        int secondCount = logger.Entries.Count(e => e.Message.Contains("nonce/hash"));

        firstCount.ShouldBe(1);
        secondCount.ShouldBe(1);
    }

    private static CspComposer BuildComposer(
        ICspContributor contributor,
        ILogger<CspComposer> logger)
    {
        CspContributorRegistry registry = new();
        registry.Add(contributor);

        GranitSecurityHeadersOptions opts = new();
        SecurityHeadersMiddlewareTests.TestOptionsMonitor<GranitSecurityHeadersOptions> monitor =
            new(opts);

        IHostEnvironment env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(Environments.Development);

        return new CspComposer(monitor, registry, env, logger);
    }

    private static DefaultHttpContext MakeContextWithMatchedEndpoint()
    {
        DefaultHttpContext ctx = new();
        ctx.SetEndpoint(new Endpoint(
            static _ => Task.CompletedTask,
            EndpointMetadataCollection.Empty,
            "test-endpoint"));
        return ctx;
    }

    private sealed class StubContributor(Action<CspBuilder> contribute) : ICspContributor
    {
        public void Contribute(HttpContext context, CspBuilder builder) => contribute(builder);
    }
}
