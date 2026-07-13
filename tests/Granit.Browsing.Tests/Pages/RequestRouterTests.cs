using System.Diagnostics.Metrics;
using System.Net;
using Granit.Browsing.Diagnostics;
using Granit.Browsing.Pages;
using Granit.Browsing.Sandbox;
using Granit.Http.UrlSafety;
using Granit.Http.UrlSafety.Options;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Browsing.Tests.Pages;

public sealed class RequestRouterTests
{
    private static RouteRequest Request(string url, string method = "GET") =>
        new(new Uri(url), method, new Dictionary<string, string>());

    private static BrowsingMetrics Metrics()
    {
        IMeterFactory factory = new ServiceCollection().AddMetrics().BuildServiceProvider().GetRequiredService<IMeterFactory>();
        return new BrowsingMetrics(factory);
    }

    private static RequestRouter CreateRouter(
        IBrowserSandboxProfile sandbox,
        IUrlSafetyValidator? urlSafety = null)
    {
        IUrlSafetyValidator validator = urlSafety ?? FakeValidator(valid: true);
        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(DateTimeOffset.UnixEpoch);
        return new RequestRouter(
            sandbox,
            validator,
            Metrics(),
            NullLogger<RequestRouter>.Instance,
            clock,
            engineName: "test-engine",
            pageId: Guid.NewGuid());
    }

    private static IUrlSafetyValidator FakeValidator(bool valid, UrlSafetyViolationKind kind = UrlSafetyViolationKind.PrivateNetwork)
    {
        IUrlSafetyValidator v = Substitute.For<IUrlSafetyValidator>();
        UrlSafetyResult result = valid
            ? UrlSafetyResult.Valid([IPAddress.Parse("93.184.216.34")])
            : UrlSafetyResult.Invalid(new UrlSafetyViolation(kind, "blocked", "UrlSafety:PrivateNetwork"));
#pragma warning disable CA2012 // NSubstitute configuration intentionally builds and stores a ValueTask for replay.
        v.ValidateAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>()).Returns(_ => ValueTask.FromResult(result));
        v.ValidateAsync(Arg.Any<Uri>(), Arg.Any<UrlSafetyOptions>(), Arg.Any<CancellationToken>()).Returns(_ => ValueTask.FromResult(result));
#pragma warning restore CA2012
        return v;
    }

    [Fact]
    public async Task Scheme_not_in_allowlist_should_abort()
    {
        RequestRouter router = CreateRouter(new SandboxProfile { AllowedSchemes = ["https"] });

        RouteDecision decision = await router.EvaluateAsync(Request("http://example.com/"), CancellationToken.None);

        decision.Kind.ShouldBe(RouteDecisionKind.Abort);
        decision.ErrorCode.ShouldBe("scheme_not_allowed");
    }

    [Fact]
    public async Task Allowed_host_pattern_should_filter()
    {
        RequestRouter router = CreateRouter(new SandboxProfile
        {
            AllowedHostPatterns = ["api.example.com/**"],
        });

        RouteDecision permit = await router.EvaluateAsync(Request("https://api.example.com/v1"), CancellationToken.None);
        permit.Kind.ShouldBe(RouteDecisionKind.Continue);

        RouteDecision deny = await router.EvaluateAsync(Request("https://other.example.com/"), CancellationToken.None);
        deny.Kind.ShouldBe(RouteDecisionKind.Abort);
        deny.ErrorCode.ShouldBe("host_not_allowed");
    }

    [Fact]
    public async Task Denied_host_pattern_should_block()
    {
        RequestRouter router = CreateRouter(new SandboxProfile
        {
            DeniedHostPatterns = ["evil.example.com/**"],
        });

        RouteDecision decision = await router.EvaluateAsync(Request("https://evil.example.com/foo"), CancellationToken.None);

        decision.Kind.ShouldBe(RouteDecisionKind.Abort);
        decision.ErrorCode.ShouldBe("host_denied");
    }

    [Fact]
    public async Task Private_network_validator_failure_should_block()
    {
        RequestRouter router = CreateRouter(
            new SandboxProfile { BlockPrivateNetworks = true },
            FakeValidator(valid: false));

        RouteDecision decision = await router.EvaluateAsync(Request("https://intranet.local/"), CancellationToken.None);

        decision.Kind.ShouldBe(RouteDecisionKind.Abort);
    }

    [Fact]
    public async Task User_handlers_should_run_in_registration_order()
    {
        RequestRouter router = CreateRouter(new SandboxProfile { BlockPrivateNetworks = false });

        List<string> calls = [];
        router.Register(RoutePattern.Parse("**/*"), (req, ct) =>
        {
            calls.Add("first");
            return ValueTask.FromResult(RouteDecision.Continue);
        });
        router.Register(RoutePattern.Parse("**/*"), (req, ct) =>
        {
            calls.Add("second");
            return ValueTask.FromResult(RouteDecision.Abort("by_handler"));
        });
        router.Register(RoutePattern.Parse("**/*"), (req, ct) =>
        {
            calls.Add("third");
            return ValueTask.FromResult(RouteDecision.Continue);
        });

        RouteDecision decision = await router.EvaluateAsync(Request("https://api.example.com/"), CancellationToken.None);

        decision.Kind.ShouldBe(RouteDecisionKind.Abort);
        decision.ErrorCode.ShouldBe("by_handler");
        calls.ShouldBe(["first", "second"]);
    }

    [Fact]
    public async Task Sandbox_should_take_precedence_over_user_handler()
    {
        bool userHandlerCalled = false;
        RequestRouter router = CreateRouter(new SandboxProfile { AllowedSchemes = ["https"] });
        router.Register(RoutePattern.Parse("**/*"), (req, ct) =>
        {
            userHandlerCalled = true;
            return ValueTask.FromResult(RouteDecision.Continue);
        });

        RouteDecision decision = await router.EvaluateAsync(Request("http://example.com/"), CancellationToken.None);

        decision.Kind.ShouldBe(RouteDecisionKind.Abort);
        userHandlerCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task Throwing_user_handler_should_abort_under_default_policy()
    {
        // Default RouterErrorPolicy is AbortOnError — a throwing handler fails closed.
        RequestRouter router = CreateRouter(new SandboxProfile { BlockPrivateNetworks = false });

        router.Register(RoutePattern.Parse("**/*"), (req, ct) =>
            throw new InvalidOperationException("handler boom"));

        RouteDecision decision = await router.EvaluateAsync(Request("https://api.example.com/"), CancellationToken.None);

        decision.Kind.ShouldBe(RouteDecisionKind.Abort);
        decision.ErrorCode.ShouldBe("handler_error");
    }

    [Fact]
    public async Task Pattern_mismatch_should_skip_user_handler()
    {
        bool called = false;
        RequestRouter router = CreateRouter(new SandboxProfile { BlockPrivateNetworks = false });

        router.Register(RoutePattern.Parse("evil.example.com/**"), (req, ct) =>
        {
            called = true;
            return ValueTask.FromResult(RouteDecision.Abort("nope"));
        });

        RouteDecision decision = await router.EvaluateAsync(Request("https://api.example.com/"), CancellationToken.None);

        decision.Kind.ShouldBe(RouteDecisionKind.Continue);
        called.ShouldBeFalse();
    }
}
