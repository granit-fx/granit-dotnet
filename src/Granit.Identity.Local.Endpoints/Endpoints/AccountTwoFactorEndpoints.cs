using Granit.Events;
using Granit.Http.Idempotency.Attributes;
using Granit.Http.SecurityHeaders.Extensions;
using Granit.Identity;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Identity.Local.Events;
using Granit.Identity.Local.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Identity.Local.Endpoints.Endpoints;

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

        group.MapGet("/two-factor/authenticator-key", (Delegate)GetAuthenticatorKeyAsync)
            .WithName("GetAuthenticatorKey")
            .WithSummary("Returns the TOTP shared key and QR code URI.")
            .WithDescription(
                "Generates or returns the existing TOTP shared key for the user. "
                + "The QR code URI can be rendered by the frontend for authenticator app scanning.")
            .Produces<AccountAuthenticatorKeyResponse>()
            .RequireAuthorization()
            .WithNoStoreResponse();

        group.MapPost("/two-factor/enable", EnableAsync)
            .WithName("EnableTwoFactor")
            .WithSummary("Enables 2FA after TOTP code verification.")
            .WithDescription(
                "Validates the provided TOTP code against the shared key. On success, "
                + "enables 2FA and returns single-use recovery codes. "
                + "Returns 400 if the code is invalid.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces<AccountTwoFactorEnableResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem()
            .RequireAuthorization()
            .WithNoStoreResponse();

        group.MapPost("/two-factor/disable", DisableAsync)
            .WithName("DisableTwoFactor")
            .WithSummary("Disables 2FA for the authenticated user.")
            .WithDescription(
                "Disables TOTP-based two-factor authentication. "
                + "Requires password confirmation as step-up authentication (OWASP ASVS V2.8.1).")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem()
            .RequireAuthorization();

        group.MapPost("/two-factor/recovery-codes", GenerateRecoveryCodesAsync)
            .WithName("GenerateRecoveryCodes")
            .WithSummary("Generates new recovery codes.")
            .WithDescription(
                "Generates 10 new single-use recovery codes. Previously generated codes "
                + "are invalidated. Requires password confirmation as step-up authentication. "
                + "Recovery codes can be used instead of a TOTP code during login.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces<AccountRecoveryCodesResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem()
            .RequireAuthorization()
            .WithNoStoreResponse();

        return group;
    }

    private static async Task<Ok<AccountTwoFactorStatusResponse>> GetStatusAsync(
        HttpContext httpContext,
        [FromServices] ITwoFactorService twoFactorService,
        CancellationToken cancellationToken)
    {
        string userId = httpContext.User.FindFirst("sub")!.Value;
        TwoFactorStatus status = await twoFactorService
            .GetStatusAsync(userId, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(new AccountTwoFactorStatusResponse(
            status.IsEnabled, status.HasAuthenticatorApp, status.RecoveryCodesLeft));
    }

    private static async Task<Ok<AccountAuthenticatorKeyResponse>> GetAuthenticatorKeyAsync(
        HttpContext httpContext,
        [FromServices] ITwoFactorService twoFactorService,
        CancellationToken cancellationToken)
    {
        string userId = httpContext.User.FindFirst("sub")!.Value;
        AuthenticatorKeyInfo keyInfo = await twoFactorService
            .GetAuthenticatorKeyAsync(userId, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(new AccountAuthenticatorKeyResponse(keyInfo.SharedKey, keyInfo.QrCodeUri));
    }

    private static async Task<Results<Ok<AccountTwoFactorEnableResponse>, ProblemHttpResult>> EnableAsync(
        AccountTwoFactorEnableRequest request,
        HttpContext httpContext,
        [FromServices] ITwoFactorService twoFactorService,
        CancellationToken cancellationToken)
    {
        string userId = httpContext.User.FindFirst("sub")!.Value;

        try
        {
            IReadOnlyList<string> recoveryCodes = await twoFactorService
                .EnableAsync(userId, request.Code, cancellationToken).ConfigureAwait(false);

            await PublishTwoFactorChangedAsync(httpContext, Guid.Parse(userId), true, cancellationToken)
                .ConfigureAwait(false);

            return TypedResults.Ok(new AccountTwoFactorEnableResponse(recoveryCodes));
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DisableAsync(
        AccountTwoFactorDisableRequest request,
        HttpContext httpContext,
        [FromServices] IIdentityCredentialVerifier credentialVerifier,
        [FromServices] ITwoFactorService twoFactorService,
        CancellationToken cancellationToken)
    {
        string userId = httpContext.User.FindFirst("sub")!.Value;
        string? username = httpContext.User.FindFirst("preferred_username")?.Value
                           ?? httpContext.User.FindFirst("name")?.Value;

        if (username is null)
        {
            return TypedResults.Problem(
                detail: "Unable to determine username from token claims.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        bool isValid = await credentialVerifier
            .VerifyUserCredentialsAsync(username, request.Password, cancellationToken)
            .ConfigureAwait(false);

        if (!isValid)
        {
            return TypedResults.Problem(
                detail: "Password is incorrect.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        await twoFactorService.DisableAsync(userId, cancellationToken).ConfigureAwait(false);

        await PublishTwoFactorChangedAsync(httpContext, Guid.Parse(userId), false, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<AccountRecoveryCodesResponse>, ProblemHttpResult>> GenerateRecoveryCodesAsync(
        AccountGenerateRecoveryCodesRequest request,
        HttpContext httpContext,
        [FromServices] IIdentityCredentialVerifier credentialVerifier,
        [FromServices] ITwoFactorService twoFactorService,
        CancellationToken cancellationToken)
    {
        string userId = httpContext.User.FindFirst("sub")!.Value;
        string? username = httpContext.User.FindFirst("preferred_username")?.Value
                           ?? httpContext.User.FindFirst("name")?.Value;

        if (username is null)
        {
            return TypedResults.Problem(
                detail: "Unable to determine username from token claims.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        bool isValid = await credentialVerifier
            .VerifyUserCredentialsAsync(username, request.Password, cancellationToken)
            .ConfigureAwait(false);

        if (!isValid)
        {
            return TypedResults.Problem(
                detail: "Password is incorrect.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        IReadOnlyList<string> codes = await twoFactorService
            .GenerateRecoveryCodesAsync(userId, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(new AccountRecoveryCodesResponse(codes));
    }

    private static async Task PublishTwoFactorChangedAsync(
        HttpContext httpContext,
        Guid userId,
        bool enabled,
        CancellationToken cancellationToken)
    {
        IDistributedEventBus? eventBus = httpContext.RequestServices.GetService<IDistributedEventBus>();
        if (eventBus is null)
        {
            return;
        }

        string? tenantId = httpContext.User.FindFirst("tenant_id")?.Value;
        Guid? parsedTenantId = tenantId is not null ? Guid.Parse(tenantId) : null;

        await eventBus.PublishAsync(
            new TwoFactorChangedEto(userId, enabled, parsedTenantId),
            cancellationToken).ConfigureAwait(false);
    }
}
