using System.Buffers.Text;
using System.Diagnostics.Metrics;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Granit.Authentication.Mtls.Diagnostics;
using Granit.Authentication.Mtls.Middleware;
using Granit.Authentication.Mtls.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Authentication.Mtls.Tests.Middleware;

public sealed class MtlsValidationMiddlewareTests
{
    [Fact]
    public async Task Unauthenticated_CallsNext()
    {
        (MtlsValidationMiddleware mw, HttpContext ctx, Func<bool> nextCalled) =
            Build(new ClaimsPrincipal(new ClaimsIdentity()), certificate: null);

        await mw.InvokeAsync(ctx);

        nextCalled().ShouldBeTrue();
        ctx.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task BoundToken_MatchingCertificate_CallsNext()
    {
        using X509Certificate2 cert = CreateCertificate();
        (MtlsValidationMiddleware mw, HttpContext ctx, Func<bool> nextCalled) =
            Build(AuthenticatedWithCnf(Thumbprint(cert)), cert);

        await mw.InvokeAsync(ctx);

        nextCalled().ShouldBeTrue();
        ctx.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task BoundToken_NoCertificate_Returns401()
    {
        (MtlsValidationMiddleware mw, HttpContext ctx, Func<bool> nextCalled) =
            Build(AuthenticatedWithCnf("some-thumbprint"), certificate: null);

        await mw.InvokeAsync(ctx);

        nextCalled().ShouldBeFalse();
        ctx.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task BoundToken_MismatchedCertificate_Returns401()
    {
        using X509Certificate2 cert = CreateCertificate();
        (MtlsValidationMiddleware mw, HttpContext ctx, Func<bool> nextCalled) =
            Build(AuthenticatedWithCnf("a-different-thumbprint"), cert);

        await mw.InvokeAsync(ctx);

        nextCalled().ShouldBeFalse();
        ctx.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task UnboundToken_NotRequired_CallsNext()
    {
        (MtlsValidationMiddleware mw, HttpContext ctx, Func<bool> nextCalled) =
            Build(Authenticated(), certificate: null, requireBinding: false);

        await mw.InvokeAsync(ctx);

        nextCalled().ShouldBeTrue();
    }

    [Fact]
    public async Task UnboundToken_Required_Returns401()
    {
        (MtlsValidationMiddleware mw, HttpContext ctx, Func<bool> nextCalled) =
            Build(Authenticated(), certificate: null, requireBinding: true);

        await mw.InvokeAsync(ctx);

        nextCalled().ShouldBeFalse();
        ctx.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    private static (MtlsValidationMiddleware Middleware, HttpContext Context, Func<bool> NextCalled) Build(
        ClaimsPrincipal user, X509Certificate2? certificate, bool requireBinding = false)
    {
        bool nextCalled = false;
        Task Next(HttpContext _)
        {
            nextCalled = true;
            return Task.CompletedTask;
        }

        var metrics = new MtlsValidationMetrics(new TestMeterFactory());
        IOptions<MtlsValidationOptions> options = Microsoft.Extensions.Options.Options.Create(
            new MtlsValidationOptions { RequireCertificateBinding = requireBinding });

        var middleware = new MtlsValidationMiddleware(
            Next, metrics, options, NullLogger<MtlsValidationMiddleware>.Instance);

        DefaultHttpContext context = new() { User = user };
        context.Connection.ClientCertificate = certificate;

        return (middleware, context, () => nextCalled);
    }

    private static ClaimsPrincipal Authenticated() =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-id")], "Bearer"));

    private static ClaimsPrincipal AuthenticatedWithCnf(string thumbprint) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "user-id"),
                new Claim("cnf", $$"""{"x5t#S256":"{{thumbprint}}"}"""),
            ],
            "Bearer"));

    private static string Thumbprint(X509Certificate2 cert) =>
        Base64Url.EncodeToString(SHA256.HashData(cert.RawData));

    private static X509Certificate2 CreateCertificate()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=mtls-test-client", key, HashAlgorithmName.SHA256);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    private sealed class TestMeterFactory : IMeterFactory
    {
        public Meter Create(MeterOptions options) => new(options);
        public void Dispose() { }
    }
}
