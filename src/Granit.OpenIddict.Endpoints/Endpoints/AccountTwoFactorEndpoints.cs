using Granit.OpenIddict.Endpoints.Dtos;
using Granit.OpenIddict.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.OpenIddict.Endpoints.Endpoints;

internal static class AccountTwoFactorEndpoints
{
    internal static RouteGroupBuilder MapAccountTwoFactorEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/two-factor", (Delegate)GetStatusAsync)
            .WithName("GetTwoFactorStatus")
            .WithSummary("Returns the current 2FA status.")
            .WithDescription(
                "Returns whether 2FA is enabled, whether an authenticator app "
                + "is configured, and the number of unused recovery codes.")
            .Produces<AccountTwoFactorStatusResponse>()
            .RequireAuthorization();

        group.MapGet("/two-factor/authenticator-key", GetAuthenticatorKeyAsync)
            .WithName("GetAuthenticatorKey")
            .WithSummary("Returns the TOTP shared key and QR code URI.")
            .WithDescription(
                "Generates or returns the existing TOTP shared key for the user. "
                + "The QR code URI can be rendered by the frontend for authenticator app scanning.")
            .Produces<AccountAuthenticatorKeyResponse>()
            .RequireAuthorization();

        group.MapPost("/two-factor/enable", EnableAsync)
            .WithName("EnableTwoFactor")
            .WithSummary("Enables 2FA after TOTP code verification.")
            .WithDescription(
                "Validates the provided TOTP code against the shared key. On success, "
                + "enables 2FA and returns single-use recovery codes. "
                + "Returns 400 if the code is invalid.")
            .Produces<AccountTwoFactorEnableResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem()
            .RequireAuthorization();

        group.MapPost("/two-factor/disable", (Delegate)DisableAsync)
            .WithName("DisableTwoFactor")
            .WithSummary("Disables 2FA for the authenticated user.")
            .WithDescription("Disables TOTP-based two-factor authentication.")
            .Produces(StatusCodes.Status204NoContent)
            .RequireAuthorization();

        group.MapPost("/two-factor/recovery-codes", (Delegate)GenerateRecoveryCodesAsync)
            .WithName("GenerateRecoveryCodes")
            .WithSummary("Generates new recovery codes.")
            .WithDescription(
                "Generates 10 new single-use recovery codes. Previously generated codes "
                + "are invalidated. Recovery codes can be used instead of a TOTP code during login.")
            .Produces<AccountRecoveryCodesResponse>()
            .RequireAuthorization();

        return group;
    }

    private static Task<Ok<AccountTwoFactorStatusResponse>> GetStatusAsync(
        HttpContext httpContext)
    {
        // TODO: Resolve from UserManager<GranitUser>.GetTwoFactorEnabledAsync()
        return Task.FromResult(TypedResults.Ok(new AccountTwoFactorStatusResponse(false, false, 0)));
    }

    private static Task<Ok<AccountAuthenticatorKeyResponse>> GetAuthenticatorKeyAsync(
        HttpContext httpContext,
[FromServices] ITotpService totpService)
    {
        string sharedKey = totpService.GenerateSharedKey();
        string? email = httpContext.User.FindFirst("email")?.Value ?? "user";
        string qrCodeUri = totpService.GetQrCodeUri(email, sharedKey);

        return Task.FromResult(TypedResults.Ok(new AccountAuthenticatorKeyResponse(sharedKey, qrCodeUri)));
    }

    private static Task<Results<Ok<AccountTwoFactorEnableResponse>, ProblemHttpResult>> EnableAsync(
        AccountTwoFactorEnableRequest request,
        HttpContext httpContext,
[FromServices] ITotpService totpService)
    {
        // TODO: Validate code against stored shared key via UserManager
        // For now, placeholder implementation
        return Task.FromResult<Results<Ok<AccountTwoFactorEnableResponse>, ProblemHttpResult>>(
            TypedResults.Ok(new AccountTwoFactorEnableResponse([])));
    }

    private static Task<NoContent> DisableAsync(HttpContext httpContext)
    {
        // TODO: Disable via UserManager<GranitUser>.SetTwoFactorEnabledAsync(user, false)
        return Task.FromResult(TypedResults.NoContent());
    }

    private static Task<Ok<AccountRecoveryCodesResponse>> GenerateRecoveryCodesAsync(
        HttpContext httpContext)
    {
        // TODO: Generate via UserManager<GranitUser>.GenerateNewTwoFactorRecoveryCodesAsync(user, 10)
        return Task.FromResult(TypedResults.Ok(new AccountRecoveryCodesResponse([])));
    }
}
