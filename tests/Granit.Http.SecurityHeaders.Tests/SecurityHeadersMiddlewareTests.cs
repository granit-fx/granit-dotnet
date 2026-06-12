using Granit.Http.SecurityHeaders.Contributors;
using Granit.Http.SecurityHeaders.Internal;
using Granit.Http.SecurityHeaders.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.SecurityHeaders.Tests;

public sealed class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task Middleware_AddsDefaultScalarHeaders()
    {
        TestHarness harness = new();
        await harness.Invoke();

        harness.Response.Headers.XContentTypeOptions.ToString().ShouldBe("nosniff");
        harness.Response.Headers.XFrameOptions.ToString().ShouldBe("DENY");
        harness.Response.Headers["Referrer-Policy"].ToString()
            .ShouldBe("strict-origin-when-cross-origin");
        harness.Response.Headers.XXSSProtection.ToString().ShouldBe("0");
        harness.Response.Headers["Permissions-Policy"].ToString()
            .ShouldBe("camera=(), microphone=(), geolocation=(), payment=(), " +
                       "accelerometer=(), gyroscope=(), magnetometer=(), usb=()");
        harness.Response.Headers["Cross-Origin-Opener-Policy"].ToString()
            .ShouldBe("same-origin");
        harness.Response.Headers["Cross-Origin-Resource-Policy"].ToString()
            .ShouldBe("same-origin");
    }

    [Fact]
    public async Task Middleware_EmitsApiGradeCsp_ByDefault_AfterOnStarting()
    {
        TestHarness harness = new();
        await harness.InvokeAndFlush();

        harness.Response.Headers.ContentSecurityPolicy.ToString()
            .ShouldBe("default-src 'none'; base-uri 'none'; frame-ancestors 'none'");
    }

    [Fact]
    public async Task Middleware_EmitsRawOverride_VerbatimWhenSet()
    {
        TestHarness harness = new(o => o.Csp.RawOverride = "default-src 'self'");
        await harness.InvokeAndFlush();

        harness.Response.Headers.ContentSecurityPolicy.ToString().ShouldBe("default-src 'self'");
    }

    [Fact]
    public async Task Middleware_OmitsXFrameOptions_WhenNull()
    {
        TestHarness harness = new(o => o.XFrameOptions = null);
        await harness.Invoke();

        harness.Response.Headers.ContainsKey("X-Frame-Options").ShouldBeFalse();
    }

    [Fact]
    public async Task Middleware_OmitsCrossOriginEmbedderPolicy_WhenNull()
    {
        TestHarness harness = new();
        await harness.Invoke();

        harness.Response.Headers.ContainsKey("Cross-Origin-Embedder-Policy").ShouldBeFalse();
    }

    [Fact]
    public async Task Middleware_AddsCrossOriginEmbedderPolicy_WhenConfigured()
    {
        TestHarness harness = new(o => o.CrossOriginEmbedderPolicy = "require-corp");
        await harness.Invoke();

        harness.Response.Headers["Cross-Origin-Embedder-Policy"].ToString().ShouldBe("require-corp");
    }

    [Fact]
    public async Task Middleware_DisablesAllHeaders_WhenConfiguredOff()
    {
        TestHarness harness = new(o =>
        {
            o.EnableContentTypeOptions = false;
            o.XFrameOptions = null;
            o.DisableXssProtection = false;
            o.PermissionsPolicy = null;
            o.ReferrerPolicy = "";
            o.CrossOriginOpenerPolicy = "";
            o.CrossOriginResourcePolicy = "";
        });
        await harness.Invoke();

        harness.Response.Headers.ContainsKey("X-Content-Type-Options").ShouldBeFalse();
        harness.Response.Headers.ContainsKey("X-Frame-Options").ShouldBeFalse();
        harness.Response.Headers.ContainsKey("X-XSS-Protection").ShouldBeFalse();
        harness.Response.Headers.ContainsKey("Permissions-Policy").ShouldBeFalse();
        harness.Response.Headers.ContainsKey("Referrer-Policy").ShouldBeFalse();
        harness.Response.Headers.ContainsKey("Cross-Origin-Opener-Policy").ShouldBeFalse();
        harness.Response.Headers.ContainsKey("Cross-Origin-Resource-Policy").ShouldBeFalse();
    }

    [Fact]
    public async Task Middleware_CallsNextDelegate()
    {
        bool nextCalled = false;
        TestHarness harness = new(next: _ => { nextCalled = true; return Task.CompletedTask; });

        await harness.Invoke();

        nextCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task Middleware_ReappliesHeaders_AfterResponseClear()
    {
        TestHarness harness = new(next: ctx =>
        {
            ctx.Response.Headers.Clear();
            return Task.CompletedTask;
        });

        await harness.Invoke();
        harness.Response.Headers.ContainsKey("X-Content-Type-Options").ShouldBeFalse();

        await harness.FireOnStarting();

        harness.Response.Headers.XContentTypeOptions.ToString().ShouldBe("nosniff");
        harness.Response.Headers.XFrameOptions.ToString().ShouldBe("DENY");
        harness.Response.Headers["Referrer-Policy"].ToString()
            .ShouldBe("strict-origin-when-cross-origin");
    }

    [Fact]
    public async Task Middleware_RunsContributors_ForMatchedEndpoint()
    {
        StubContributor contributor = new(
            applies: endpoint => endpoint?.Metadata.GetMetadata<StubMarker>() is not null,
            contribute: b => b.AddScriptSrc("'self'", "'unsafe-inline'"));

        TestHarness harness = new(contributors: [contributor]);
        harness.SetEndpoint(new Endpoint(static _ => Task.CompletedTask,
            new EndpointMetadataCollection(new StubMarker()), "scalar-test"));

        await harness.InvokeAndFlush();

        string csp = harness.Response.Headers.ContentSecurityPolicy.ToString();
        csp.ShouldContain("script-src 'self' 'unsafe-inline'");
    }

    [Fact]
    public async Task Middleware_SkipsContributors_DisabledByName()
    {
        StubContributor contributor = new(
            applies: _ => true,
            contribute: b => b.AddScriptSrc("'self'"),
            name: "MyContrib");

        TestHarness harness = new(
            configure: o => o.DisabledContributors = ["MyContrib"],
            contributors: [contributor]);
        harness.SetEndpoint(new Endpoint(static _ => Task.CompletedTask,
            EndpointMetadataCollection.Empty, "any"));

        await harness.InvokeAndFlush();

        harness.Response.Headers.ContentSecurityPolicy.ToString()
            .ShouldNotContain("script-src");
    }

    [Fact]
    public async Task Middleware_ContributorOnDifferentEndpoint_DoesNotLeak()
    {
        StubContributor contributor = new(
            applies: endpoint => endpoint?.Metadata.GetMetadata<StubMarker>() is not null,
            contribute: b => b.AddScriptSrc("'self'", "'unsafe-inline'"));

        TestHarness harness = new(contributors: [contributor]);
        // Endpoint without marker → contributor should not apply.
        harness.SetEndpoint(new Endpoint(static _ => Task.CompletedTask,
            EndpointMetadataCollection.Empty, "plain"));

        await harness.InvokeAndFlush();

        harness.Response.Headers.ContentSecurityPolicy.ToString()
            .ShouldBe("default-src 'none'; base-uri 'none'; frame-ancestors 'none'");
    }

    [Fact]
    public async Task Middleware_RemovesAnyExistingCsp_BeforeWriting()
    {
        // Single-header guarantee: if upstream code wrote a CSP, the composer
        // must Remove it before writing the composed one — multi-header CSP
        // is intersected by browsers and silently neutralises contributor
        // relaxations.
        StubContributor contributor = new(
            applies: _ => true,
            contribute: b => b.AddScriptSrc("'self'", "'unsafe-inline'"));

        TestHarness harness = new(next: ctx =>
        {
            ctx.Response.Headers.ContentSecurityPolicy = "default-src 'attacker.example'";
            return Task.CompletedTask;
        }, contributors: [contributor]);
        harness.SetEndpoint(new Endpoint(static _ => Task.CompletedTask,
            EndpointMetadataCollection.Empty, "any"));

        await harness.InvokeAndFlush();

        // Exactly one CSP header, and it's the composed one (not the bogus,
        // not an intersection).
        string[] cspValues = harness.Response.Headers.ContentSecurityPolicy.ToArray()!;
        cspValues.Length.ShouldBe(1);
        cspValues[0].ShouldContain("script-src 'self' 'unsafe-inline'");
        cspValues[0].ShouldNotContain("attacker.example");
    }

    [Fact]
    public void ApplyScalarHeaders_IsIdempotent()
    {
        DefaultHttpContext ctx = new();
        GranitSecurityHeadersOptions options = new();

        SecurityHeadersMiddleware.ApplyScalarHeaders(ctx, options);
        SecurityHeadersMiddleware.ApplyScalarHeaders(ctx, options);

        ctx.Response.Headers.XContentTypeOptions.ToString().ShouldBe("nosniff");
        ctx.Response.Headers.XFrameOptions.ToString().ShouldBe("DENY");
    }

    [Fact]
    public async Task Middleware_AppliesDefaultCoop_OnEndpointWithoutPopupMarker()
    {
        TestHarness harness = new();
        harness.SetEndpoint(new Endpoint(static _ => Task.CompletedTask,
            EndpointMetadataCollection.Empty, "plain"));

        await harness.InvokeAndFlush();

        harness.Response.Headers["Cross-Origin-Opener-Policy"].ToString().ShouldBe("same-origin");
    }

    [Fact]
    public async Task Middleware_DowngradesCoopToUnsafeNone_OnEndpointWithPopupMarker()
    {
        TestHarness harness = new();
        harness.SetEndpoint(new Endpoint(static _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AllowsPopupAuthorizationMetadata()),
            "popup-oauth"));

        await harness.InvokeAndFlush();

        harness.Response.Headers["Cross-Origin-Opener-Policy"].ToString().ShouldBe("unsafe-none");
    }

    [Fact]
    public async Task Middleware_PopupMarkerOnOneEndpoint_DoesNotLeakToOthers()
    {
        // Same harness, two consecutive requests on different endpoints.
        TestHarness harness = new();

        harness.SetEndpoint(new Endpoint(static _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AllowsPopupAuthorizationMetadata()),
            "popup-oauth"));
        await harness.InvokeAndFlush();
        harness.Response.Headers["Cross-Origin-Opener-Policy"].ToString().ShouldBe("unsafe-none");

        TestHarness harness2 = new();
        harness2.SetEndpoint(new Endpoint(static _ => Task.CompletedTask,
            EndpointMetadataCollection.Empty, "plain"));
        await harness2.InvokeAndFlush();
        harness2.Response.Headers["Cross-Origin-Opener-Policy"].ToString().ShouldBe("same-origin");
    }

    // ===== test harness ===================================================

    private sealed class StubMarker;

    private sealed class StubContributor(
        Func<Endpoint?, bool> applies,
        Action<CspBuilder> contribute,
        string? name = null) : ICspContributor
    {
        public string Name => name ?? nameof(StubContributor);

        public void Contribute(HttpContext context, CspBuilder builder)
        {
            if (applies(context.GetEndpoint()))
            {
                contribute(builder);
            }
        }
    }

    private sealed class TestHarness
    {
        public DefaultHttpContext Context { get; }
        public HttpResponse Response => Context.Response;
        public SecurityHeadersMiddleware Middleware { get; }
        private readonly CallbackCapturingResponseFeature _responseFeature;

        public TestHarness(
            Action<GranitSecurityHeadersOptions>? configure = null,
            RequestDelegate? next = null,
            IList<ICspContributor>? contributors = null)
        {
            GranitSecurityHeadersOptions options = new();
            configure?.Invoke(options);

            FeatureCollection features = new();
            _responseFeature = new CallbackCapturingResponseFeature();
            features.Set<IHttpResponseFeature>(_responseFeature);
            features.Set<IHttpRequestFeature>(new HttpRequestFeature());
            features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(Stream.Null));
            Context = new DefaultHttpContext(features);

            CspContributorRegistry registry = new();
            if (contributors is not null)
            {
                foreach (ICspContributor c in contributors)
                {
                    registry.Add(c);
                }
            }

            IHostEnvironment env = Substitute.For<IHostEnvironment>();
            env.EnvironmentName.Returns(Environments.Development);

            TestOptionsMonitor<GranitSecurityHeadersOptions> monitor = new(options);
            CspComposer composer = new(monitor, registry, env, NullLogger<CspComposer>.Instance);

            Middleware = new SecurityHeadersMiddleware(
                next ?? (_ => Task.CompletedTask),
                monitor,
                composer);
        }

        public void SetEndpoint(Endpoint endpoint) =>
            Context.SetEndpoint(endpoint);

        public Task Invoke() => Middleware.InvokeAsync(Context);

        public async Task InvokeAndFlush()
        {
            await Invoke();
            await FireOnStarting();
        }

        public async Task FireOnStarting()
        {
            foreach ((Func<object, Task> callback, object state) in _responseFeature.Callbacks)
            {
                await callback(state);
            }
        }
    }

    private sealed class CallbackCapturingResponseFeature : IHttpResponseFeature
    {
        public List<(Func<object, Task> Callback, object State)> Callbacks { get; } = [];
        public int StatusCode { get; set; } = 200;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = Stream.Null;
        public bool HasStarted => false;

        public void OnStarting(Func<object, Task> callback, object state) =>
            Callbacks.Add((callback, state));

        public void OnCompleted(Func<object, Task> callback, object state) { }
    }

    internal sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T> where T : class
    {
        private readonly List<Action<T, string?>> _listeners = [];

        public T CurrentValue { get; private set; } = value;
        public T Get(string? name) => CurrentValue;

        public IDisposable OnChange(Action<T, string?> listener)
        {
            _listeners.Add(listener);
            return new Subscription(_listeners, listener);
        }

        public void TriggerChange(T newValue)
        {
            CurrentValue = newValue;
            foreach (Action<T, string?> listener in _listeners.ToArray())
            {
                listener(newValue, null);
            }
        }

        private sealed class Subscription(List<Action<T, string?>> listeners, Action<T, string?> listener)
            : IDisposable
        {
            public void Dispose() => listeners.Remove(listener);
        }
    }
}
