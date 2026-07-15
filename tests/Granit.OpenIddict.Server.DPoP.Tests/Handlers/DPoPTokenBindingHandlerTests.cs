using System.Security.Claims;
using System.Text.Json;
using Granit.Authentication.DPoP.Validation;
using Granit.OpenIddict.Options;
using Granit.OpenIddict.Server.DPoP.Handlers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using Shouldly;
using Xunit;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace Granit.OpenIddict.Server.DPoP.Tests.Handlers;

public sealed class DPoPTokenBindingHandlerTests
{
    private const string Thumbprint = "vQ8L3pK5n7M9YpQRzZ8U7w0XAB1cD2eF3gH4iJ5kL6m";

    [Fact]
    public async Task NoDPoPHeader_OutsideFapi2_LeavesPrincipalUntouched()
    {
        IDPoPProofValidator validator = Substitute.For<IDPoPProofValidator>();
        DPoPTokenBindingHandler handler = Build(validator, fapi2: false);
        ProcessSignInContext context = BuildContext(dpopHeader: null);

        await handler.HandleAsync(context);

        context.IsRejected.ShouldBeFalse();
        context.Principal!.FindFirst(OpenIddictConstants.Claims.Confirmation).ShouldBeNull();
        await validator.DidNotReceiveWithAnyArgs()
            .ValidateAsync(default!, default!, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDPoPHeader_WithFapi2_RejectsAsInvalidDPoPProof()
    {
        IDPoPProofValidator validator = Substitute.For<IDPoPProofValidator>();
        DPoPTokenBindingHandler handler = Build(validator, fapi2: true);
        ProcessSignInContext context = BuildContext(dpopHeader: null);

        await handler.HandleAsync(context);

        context.IsRejected.ShouldBeTrue();
        context.Error.ShouldBe("invalid_dpop_proof");
    }

    [Fact]
    public async Task ValidProof_StampsCnfJktClaim()
    {
        IDPoPProofValidator validator = Substitute.For<IDPoPProofValidator>();
        validator.ValidateAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(DPoPValidationResult_Success(Thumbprint));

        DPoPTokenBindingHandler handler = Build(validator, fapi2: true);
        ProcessSignInContext context = BuildContext(dpopHeader: "fake.proof.jwt");

        await handler.HandleAsync(context);

        context.IsRejected.ShouldBeFalse();
        Claim? cnf = context.Principal!.FindFirst(OpenIddictConstants.Claims.Confirmation);
        cnf.ShouldNotBeNull();
        using var doc = JsonDocument.Parse(cnf.Value);
        doc.RootElement.GetProperty("jkt").GetString().ShouldBe(Thumbprint);
    }

    [Fact]
    public async Task InvalidProof_RejectsAsInvalidDPoPProof()
    {
        IDPoPProofValidator validator = Substitute.For<IDPoPProofValidator>();
        validator.ValidateAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DPoPValidationResult(IsValid: false, JwkThumbprint: null, Error: "Invalid proof signature."));

        DPoPTokenBindingHandler handler = Build(validator, fapi2: false);
        ProcessSignInContext context = BuildContext(dpopHeader: "fake.proof.jwt");

        await handler.HandleAsync(context);

        context.IsRejected.ShouldBeTrue();
        context.Error.ShouldBe("invalid_dpop_proof");
        context.Principal!.FindFirst(OpenIddictConstants.Claims.Confirmation).ShouldBeNull();
    }

    [Fact]
    public async Task ValidProof_WithExistingCnf_ReplacesNotAppends()
    {
        IDPoPProofValidator validator = Substitute.For<IDPoPProofValidator>();
        validator.ValidateAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(DPoPValidationResult_Success(Thumbprint));

        DPoPTokenBindingHandler handler = Build(validator, fapi2: true);
        ProcessSignInContext context = BuildContext(dpopHeader: "fake.proof.jwt");
        // Simulate a refresh_token grant: the rebuilt principal already carries a stale cnf.
        context.Principal!.Identities.First().AddClaim(
            new Claim(OpenIddictConstants.Claims.Confirmation, """{"jkt":"stale-thumbprint"}""", "JSON"));

        await handler.HandleAsync(context);

        var cnfClaims = context.Principal!.FindAll(OpenIddictConstants.Claims.Confirmation).ToList();
        cnfClaims.Count.ShouldBe(1);
        using var doc = JsonDocument.Parse(cnfClaims[0].Value);
        doc.RootElement.GetProperty("jkt").GetString().ShouldBe(Thumbprint);
    }

    [Fact]
    public async Task ValidProof_BuildsHtu_IncludingPathBase()
    {
        IDPoPProofValidator validator = Substitute.For<IDPoPProofValidator>();
        validator.ValidateAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(DPoPValidationResult_Success(Thumbprint));

        DPoPTokenBindingHandler handler = Build(validator, fapi2: true);
        ProcessSignInContext context = BuildContext(dpopHeader: "fake.proof.jwt", pathBase: "/auth");

        await handler.HandleAsync(context);

        await validator.Received(1).ValidateAsync(
            "fake.proof.jwt", "POST", "https://auth.example.com/auth/connect/token",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuthorizationEndpoint_WithFapi2_NoHeader_DoesNotReject()
    {
        // RFC 9449: DPoP proofs are presented only at the token endpoint. An interactive
        // /connect/authorize sign-in carries no DPoP header and must not be rejected even
        // under FAPI 2.0 — otherwise the authorization-code flow breaks.
        IDPoPProofValidator validator = Substitute.For<IDPoPProofValidator>();
        DPoPTokenBindingHandler handler = Build(validator, fapi2: true);
        ProcessSignInContext context = BuildContext(
            dpopHeader: null, endpointType: OpenIddictServerEndpointType.Authorization);

        await handler.HandleAsync(context);

        context.IsRejected.ShouldBeFalse();
        context.Principal!.FindFirst(OpenIddictConstants.Claims.Confirmation).ShouldBeNull();
        await validator.DidNotReceiveWithAnyArgs()
            .ValidateAsync(default!, default!, default!, TestContext.Current.CancellationToken);
    }

    private static DPoPTokenBindingHandler Build(IDPoPProofValidator validator, bool fapi2) =>
        new(
            validator,
            Microsoft.Extensions.Options.Options.Create(
                new GranitOpenIddictOptions { EnableFapi2Profile = fapi2 }),
            NullLogger<DPoPTokenBindingHandler>.Instance);

    private static ProcessSignInContext BuildContext(
        string? dpopHeader,
        OpenIddictServerEndpointType endpointType = OpenIddictServerEndpointType.Token,
        string pathBase = "")
    {
        DefaultHttpContext httpContext = new();
        httpContext.Request.Method = "POST";
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("auth.example.com");
        httpContext.Request.PathBase = pathBase;
        httpContext.Request.Path = "/connect/token";
        if (dpopHeader is not null)
        {
            httpContext.Request.Headers["DPoP"] = dpopHeader;
        }

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

    private static DPoPValidationResult DPoPValidationResult_Success(string thumbprint) =>
        new(IsValid: true, JwkThumbprint: thumbprint, Error: null);
}
