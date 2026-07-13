using Granit.Diagnostics.Endpoints.Dtos;
using Granit.Diagnostics.Endpoints.Endpoints;
using Granit.Http.SecurityHeaders;
using Granit.Http.SecurityHeaders.Contributors;
using Granit.Http.SecurityHeaders.Internal;
using Granit.Http.SecurityHeaders.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Diagnostics.Endpoints.Tests;

public sealed class CspAuditEndpointsTests
{
    [Fact]
    public void Audit_NoContributors_AllEndpointsReturnStrictBaseCsp()
    {
        using TestHarness h = new();
        h.AddEndpoint("GET", "/api/foo");
        h.AddEndpoint("POST", "/api/bar");

        CspAuditResponse response = h.Invoke();

        response.Contributors.ShouldBeEmpty();
        response.Endpoints.Count.ShouldBe(2);
        foreach (CspEndpointAudit ep in response.Endpoints)
        {
            ep.ComposedCsp.ShouldBe("default-src 'none'; base-uri 'none'; frame-ancestors 'none'");
            ep.HeaderName.ShouldBe("Content-Security-Policy");
        }
    }

    [Fact]
    public void Audit_WithContributor_OnlyMarkedEndpointShowsRelaxedCsp()
    {
        using TestHarness h = new(contributors:
        [
            new MarkerScopedContributor(b => b.AddScriptSrc("'self'", "'unsafe-inline'")),
        ]);
        h.AddEndpoint("GET", "/api/foo");
        h.AddEndpoint("GET", "/scalar", metadata: new ScalarMarker());

        CspAuditResponse response = h.Invoke();

        response.Contributors.Count.ShouldBe(1);
        response.Contributors[0].Name.ShouldBe(nameof(MarkerScopedContributor));

        CspEndpointAudit foo = response.Endpoints.Single(e => e.Pattern == "/api/foo");
        foo.ComposedCsp.ShouldNotContain("script-src");

        CspEndpointAudit scalar = response.Endpoints.Single(e => e.Pattern == "/scalar");
        scalar.ComposedCsp.ShouldContain("script-src 'self' 'unsafe-inline'");
    }

    [Fact]
    public void Audit_ReturnsBaseDirectives_FromCspOptions()
    {
        using TestHarness h = new(configure: o =>
        {
            o.Csp.DefaultSrc = ["'self'"];
            o.Csp.ScriptSrc = ["'self'", "https://cdn.example"];
        });

        CspAuditResponse response = h.Invoke();

        response.BaseDirectives.ShouldContainKey("default-src");
        response.BaseDirectives["default-src"].ShouldBe(["'self'"]);
        response.BaseDirectives.ShouldContainKey("script-src");
        response.BaseDirectives["script-src"].ShouldBe(["'self'", "https://cdn.example"]);
    }

    [Fact]
    public void Audit_ReportOnly_FlagsHeaderName()
    {
        using TestHarness h = new(configure: o => o.Csp.ReportOnly = true);
        h.AddEndpoint("GET", "/api/foo");

        CspAuditResponse response = h.Invoke();

        response.ReportOnly.ShouldBeTrue();
        response.Endpoints[0].HeaderName.ShouldBe("Content-Security-Policy-Report-Only");
    }

    [Fact]
    public void Audit_RawOverride_ReflectedInResponseAndAppliedPerEndpoint()
    {
        using TestHarness h = new(configure: o => o.Csp.RawOverride = "default-src 'self'");
        h.AddEndpoint("GET", "/api/foo");

        CspAuditResponse response = h.Invoke();

        response.RawOverride.ShouldBe("default-src 'self'");
        response.Endpoints[0].ComposedCsp.ShouldBe("default-src 'self'");
    }

    [Fact]
    public void Audit_ContributorInfo_IncludesFullTypeName()
    {
        using TestHarness h = new(contributors: [new MarkerScopedContributor(_ => { })]);

        CspAuditResponse response = h.Invoke();

        CspContributorInfo info = response.Contributors.Single();
        info.Name.ShouldBe(nameof(MarkerScopedContributor));
        info.FullTypeName.ShouldContain(nameof(MarkerScopedContributor));
        info.FullTypeName.ShouldContain(".");  // Full namespace path
    }

    // ===== harness ========================================================

    private sealed class ScalarMarker;

    private sealed class MarkerScopedContributor(Action<CspBuilder> contribute) : ICspContributor
    {
        public void Contribute(HttpContext context, CspBuilder builder)
        {
            if (context.GetEndpoint()?.Metadata.GetMetadata<ScalarMarker>() is not null)
            {
                contribute(builder);
            }
        }
    }

    private sealed class TestHarness : IDisposable
    {
        private readonly List<Endpoint> _endpoints = [];
        private readonly CspContributorRegistry _registry = new();
        private readonly GranitSecurityHeadersOptions _options = new();
        private readonly CspComposer _composer;

        public TestHarness(
            Action<GranitSecurityHeadersOptions>? configure = null,
            IList<ICspContributor>? contributors = null)
        {
            configure?.Invoke(_options);

            if (contributors is not null)
            {
                foreach (ICspContributor c in contributors)
                {
                    _registry.Add(c);
                }
            }

            IHostEnvironment env = Substitute.For<IHostEnvironment>();
            env.EnvironmentName.Returns(Environments.Development);

            IOptionsMonitor<GranitSecurityHeadersOptions> monitor =
                Substitute.For<IOptionsMonitor<GranitSecurityHeadersOptions>>();
            monitor.CurrentValue.Returns(_options);

            _composer = new CspComposer(monitor, _registry, env, NullLogger<CspComposer>.Instance);
        }

        public void AddEndpoint(string method, string pattern, params object[] metadata)
        {
            List<object> all = [.. metadata, new HttpMethodMetadata([method])];
            RouteEndpoint ep = new(
                requestDelegate: static _ => Task.CompletedTask,
                routePattern: RoutePatternFactory.Parse(pattern),
                order: 0,
                metadata: new EndpointMetadataCollection(all),
                displayName: $"{method} {pattern}");
            _endpoints.Add(ep);
        }

        public void Dispose() => _composer.Dispose();

        public CspAuditResponse Invoke()
        {
            EndpointDataSource dataSource = new TestEndpointDataSource(_endpoints);

            IOptionsMonitor<GranitSecurityHeadersOptions> monitor =
                Substitute.For<IOptionsMonitor<GranitSecurityHeadersOptions>>();
            monitor.CurrentValue.Returns(_options);

            return CspAuditEndpoints.HandleGetCspAudit(dataSource, _registry, _composer, monitor).Value!;
        }

        private sealed class TestEndpointDataSource(IReadOnlyList<Endpoint> endpoints) : EndpointDataSource
        {
            public override IReadOnlyList<Endpoint> Endpoints { get; } = endpoints;
            public override Microsoft.Extensions.Primitives.IChangeToken GetChangeToken() =>
                new Microsoft.Extensions.Primitives.CancellationChangeToken(default);
        }
    }
}
