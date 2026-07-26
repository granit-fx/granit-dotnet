using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Granit.OpenIddict.Options;
using Granit.OpenIddict.Server.Mtls.Handlers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using Shouldly;
using Xunit;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace Granit.OpenIddict.Server.Mtls.Tests.Handlers;

public sealed class MtlsTokenBindingHandlerTests
{
    [Fact]
    public async Task NoCertificate_OutsideFapi2_LeavesPrincipalUntouched()
    {
        MtlsTokenBindingHandler handler = Build(fapi2: false);
        ProcessSignInContext context = BuildContext(certificate: null);

        await handler.HandleAsync(context);

        context.IsRejected.ShouldBeFalse();
        context.Principal!.FindFirst(OpenIddictConstants.Claims.Confirmation).ShouldBeNull();
    }

    [Fact]
    public async Task NoCertificate_WithFapi2_RejectsAsInvalidRequest()
    {
        MtlsTokenBindingHandler handler = Build(fapi2: true);
        ProcessSignInContext context = BuildContext(certificate: null);

        await handler.HandleAsync(context);

        context.IsRejected.ShouldBeTrue();
        context.Error.ShouldBe(OpenIddictConstants.Errors.InvalidRequest);
    }

    [Fact]
    public async Task WithCertificate_StampsCnfX5tS256Claim()
    {
        using X509Certificate2 cert = CreateSelfSignedCertificate();
        MtlsTokenBindingHandler handler = Build(fapi2: true);
        ProcessSignInContext context = BuildContext(cert);

        await handler.HandleAsync(context);

        context.IsRejected.ShouldBeFalse();
        Claim? cnf = context.Principal!.FindFirst(OpenIddictConstants.Claims.Confirmation);
        cnf.ShouldNotBeNull();
        using var doc = JsonDocument.Parse(cnf.Value);
        doc.RootElement.GetProperty("x5t#S256").GetString().ShouldBe(ExpectedThumbprint(cert));
    }

    [Fact]
    public async Task WithCertificate_ExistingCnf_ReplacesNotAppends()
    {
        using X509Certificate2 cert = CreateSelfSignedCertificate();
        MtlsTokenBindingHandler handler = Build(fapi2: true);
        ProcessSignInContext context = BuildContext(cert);
        // Simulate a refresh_token grant: the rebuilt principal already carries a stale cnf.
        context.Principal!.Identities.First().AddClaim(
            new Claim(OpenIddictConstants.Claims.Confirmation, """{"x5t#S256":"stale"}""", "JSON"));

        await handler.HandleAsync(context);

        var cnfClaims = context.Principal!.FindAll(OpenIddictConstants.Claims.Confirmation).ToList();
        cnfClaims.Count.ShouldBe(1);
        using var doc = JsonDocument.Parse(cnfClaims[0].Value);
        doc.RootElement.GetProperty("x5t#S256").GetString().ShouldBe(ExpectedThumbprint(cert));
    }

    [Fact]
    public async Task AuthorizationEndpoint_WithFapi2_NoCertificate_DoesNotReject()
    {
        // RFC 8705 binds tokens at the token endpoint only. An interactive /connect/authorize
        // sign-in carries no client certificate and must not be rejected under FAPI 2.0.
        MtlsTokenBindingHandler handler = Build(fapi2: true);
        ProcessSignInContext context = BuildContext(
            certificate: null, endpointType: OpenIddictServerEndpointType.Authorization);

        await handler.HandleAsync(context);

        context.IsRejected.ShouldBeFalse();
        context.Principal!.FindFirst(OpenIddictConstants.Claims.Confirmation).ShouldBeNull();
    }

    private static MtlsTokenBindingHandler Build(bool fapi2) =>
        new(
            Microsoft.Extensions.Options.Options.Create(
                new GranitOpenIddictOptions { EnableFapi2Profile = fapi2 }),
            NullLogger<MtlsTokenBindingHandler>.Instance);

    private static ProcessSignInContext BuildContext(
        X509Certificate2? certificate,
        OpenIddictServerEndpointType endpointType = OpenIddictServerEndpointType.Token)
    {
        DefaultHttpContext httpContext = new();
        httpContext.Request.Method = "POST";
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("auth.example.com");
        httpContext.Request.Path = "/connect/token";
        httpContext.Connection.ClientCertificate = certificate;

        OpenIddictServerTransaction transaction = new()
        {
            EndpointType = endpointType,
            Request = new OpenIddictRequest(),
            Response = new OpenIddictResponse(),
        };
        // OpenIddictServerAspNetCoreHelpers.GetHttpRequest() reads a WeakReference<HttpRequest> by full type name.
        transaction.Properties[typeof(HttpRequest).FullName!] = new WeakReference<HttpRequest>(httpContext.Request);

        ClaimsPrincipal principal = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-id")], "Test"));

        return new ProcessSignInContext(transaction)
        {
            Principal = principal,
        };
    }

    private static string ExpectedThumbprint(X509Certificate2 cert) =>
        Base64Url.EncodeToString(SHA256.HashData(cert.RawData));

    private static X509Certificate2 CreateSelfSignedCertificate()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=mtls-test-client", key, HashAlgorithmName.SHA256);
        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }
}
