using System.Security.Claims;
using System.Text.Json;
using Granit.Authentication.DPoP.Validation;
using Granit.OpenIddict.Options;
using Granit.OpenIddict.Server.Handlers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using Shouldly;
using Xunit;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace Granit.OpenIddict.Server.Tests.Handlers;

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
    public async Task NoDPoPHeader_WithFapi2_RejectsAsInvalidRequest()
    {
        IDPoPProofValidator validator = Substitute.For<IDPoPProofValidator>();
        DPoPTokenBindingHandler handler = Build(validator, fapi2: true);
        ProcessSignInContext context = BuildContext(dpopHeader: null);

        await handler.HandleAsync(context);

        context.IsRejected.ShouldBeTrue();
        context.Error.ShouldBe(OpenIddictConstants.Errors.InvalidRequest);
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
    public async Task InvalidProof_RejectsAsInvalidRequest()
    {
        IDPoPProofValidator validator = Substitute.For<IDPoPProofValidator>();
        validator.ValidateAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DPoPValidationResult(IsValid: false, JwkThumbprint: null, Error: "Invalid proof signature."));

        DPoPTokenBindingHandler handler = Build(validator, fapi2: false);
        ProcessSignInContext context = BuildContext(dpopHeader: "fake.proof.jwt");

        await handler.HandleAsync(context);

        context.IsRejected.ShouldBeTrue();
        context.Error.ShouldBe(OpenIddictConstants.Errors.InvalidRequest);
        context.Principal!.FindFirst(OpenIddictConstants.Claims.Confirmation).ShouldBeNull();
    }

    private static DPoPTokenBindingHandler Build(IDPoPProofValidator validator, bool fapi2) =>
        new(
            validator,
            Microsoft.Extensions.Options.Options.Create(
                new GranitOpenIddictOptions { EnableFapi2Profile = fapi2 }),
            NullLogger<DPoPTokenBindingHandler>.Instance);

    private static ProcessSignInContext BuildContext(string? dpopHeader)
    {
        DefaultHttpContext httpContext = new();
        httpContext.Request.Method = "POST";
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("auth.example.com");
        httpContext.Request.Path = "/connect/token";
        if (dpopHeader is not null)
        {
            httpContext.Request.Headers["DPoP"] = dpopHeader;
        }

        OpenIddictServerTransaction transaction = new()
        {
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
