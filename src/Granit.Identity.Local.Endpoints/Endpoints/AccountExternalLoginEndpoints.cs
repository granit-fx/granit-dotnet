using System.Diagnostics;
using System.Security.Claims;
using Granit.Auditing;
using Granit.Auditing.Domain;
using Granit.Authentication.External;
using Granit.Events;
using Granit.Http.Idempotency;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Identity.Local.Endpoints.Internal;
using Granit.Identity.Local.Endpoints.Options;
using Granit.Identity.Local.Events;
using Granit.Identity.Local.Services;
using Granit.MultiTenancy;
using Granit.Settings.Services;
using Granit.Timing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ExternalLoginInfo = Granit.Identity.Local.Services.ExternalLoginInfo;
using IdentityConstants = Microsoft.AspNetCore.Identity.IdentityConstants;

namespace Granit.Identity.Local.Endpoints.Endpoints;

internal static partial class AccountExternalLoginEndpoints
{
    internal static RouteGroupBuilder MapAccountExternalLoginEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/external-logins", ListExternalLoginsAsync)
            .WithName("ListExternalLogins")
            .WithSummary("Lists linked external login providers.")
            .WithDescription(
                "Returns the list of external providers (Google, Microsoft, GitHub) "
                + "currently linked to the authenticated user's account.")
            .Produces<IReadOnlyList<ExternalLoginInfoResponse>>()
            .RequireAuthorization();

        group.MapPost("/external-logins/challenge/{provider}", ChallengeAsync)
            .WithName("ChallengeExternalLogin")
            .WithSummary("Initiates an OAuth flow with an external provider.")
            .WithDescription(
                "Validates that the specified provider is configured AND backed by a registered "
                + "authentication handler. Returns 200 to confirm the provider is available; the "
                + "frontend then initiates the OAuth redirect via the standard client challenge flow. "
                + "Returns 400 if the provider is not configured, or 500 if it is configured but no "
                + "authentication handler is registered for it (a host wiring error).")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .AllowAnonymous();

        group.MapGet("/external-logins/challenge/{provider}/start", StartChallengeAsync)
            .WithName("StartExternalLogin")
            .WithSummary("Starts the OAuth flow by redirecting to the external provider.")
            .WithDescription(
                "Issues the actual OAuth challenge: validates the provider then returns a 302 "
                + "redirect to it. The browser navigates here directly (it is not an XHR endpoint). "
                + "An optional site-relative returnUrl is carried through the external ticket so the "
                + "callback can resume the original authorization request after sign-in. "
                + "Returns 400 if the provider is not configured or has no registered handler.")
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .AllowAnonymous();

        group.MapGet("/external-logins/callback", CallbackAsync)
            .WithName("ExternalLoginCallback")
            .WithSummary("Processes the OAuth callback from an external provider.")
            .WithDescription(
                "Handles the redirect from the external provider. Authenticates an existing account "
                + "or creates a new one when account creation is enabled, then establishes the "
                + "Identity session. When the provider data is insufficient (e.g. no email), returns "
                + "a 'needs-profile-completion' result with a signed continuation token instead of "
                + "creating an account. Responds with a 302 redirect to the configured frontend URL, "
                + "or JSON when no redirect URL is configured or ?mode=json is set. "
                + "Returns 400 if the callback carries no scheme, 409 if the email is taken by "
                + "another account, 403 if account creation is disabled and no account exists.")
            .Produces<ExternalLoginCallbackResponse>()
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .AllowAnonymous();

        group.MapPost("/external-logins/complete-registration", CompleteRegistrationAsync)
            .WithName("CompleteExternalRegistration")
            .WithSummary("Completes registration started from an external provider.")
            .WithDescription(
                "Creates an account from the user-completed fields when the external provider did "
                + "not return enough data for a one-click sign-up. The provider and provider key are "
                + "taken from the signed continuation token, never from the request body. Links the "
                + "external login, establishes the session, and returns 200. "
                + "Returns 400 if the token is invalid or expired, 403 if account creation is "
                + "disabled or the token does not match the current tenant, 409 if the email is "
                + "already taken, 422 if the email does not match the provider-verified one.")
            .Produces<AccountLoginResponse>()
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .AllowAnonymous()
            .RequireRateLimiting("authentication");

        group.MapDelete("/external-logins/{provider}", UnlinkExternalLoginAsync)
            .WithName("UnlinkExternalLogin")
            .WithSummary("Unlinks an external login provider.")
            .WithDescription(
                "Removes the association between the authenticated user and the specified provider. "
                + "Returns 400 if it's the last login method and no password is set.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireAuthorization();

        return group;
    }

    private static async Task<Ok<IReadOnlyList<ExternalLoginInfoResponse>>> ListExternalLoginsAsync(
        HttpContext httpContext,
        [FromServices] IExternalLoginService externalLoginService,
        CancellationToken cancellationToken)
    {
        string userId = httpContext.User.FindFirst("sub")!.Value;
        IReadOnlyList<ExternalLoginInfo> logins = await externalLoginService
            .GetLoginsAsync(userId, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok<IReadOnlyList<ExternalLoginInfoResponse>>(
            logins.Select(IdentityLocalResponseMapper.ToResponse).ToList());
    }

    private static async Task<Results<Ok, ProblemHttpResult>> ChallengeAsync(
        string provider,
        [FromServices] IExternalProviderRegistry providerRegistry,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        // Unknown provider → client error.
        if (!providerRegistry.IsProviderConfigured(provider))
        {
            return TypedResults.Problem(
                detail: AccountEndpointMessages.Localize(
                    httpContext,
                    "Granit:Identity:ExternalLogin:ProviderNotConfigured",
                    "The specified external login provider is not configured."),
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Configured but no authentication handler wired (host forgot AddGoogle()/etc.).
        // Surface it as a server misconfiguration rather than returning a misleading 200
        // that dead-ends when the frontend initiates the redirect on a non-existent scheme.
        if (!await providerRegistry.IsProviderAvailableAsync(provider, cancellationToken).ConfigureAwait(false))
        {
            ILogger logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("Granit.Identity.Local.Endpoints.AccountExternalLoginEndpoints");
            LogProviderSchemeMissing(logger, provider);

            return TypedResults.Problem(
                detail: "The external login provider is configured but no authentication handler "
                    + "is registered for it. Register the provider's authentication scheme on the host "
                    + "(e.g. AddGoogle()/AddMicrosoftAccount()).",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        // The actual OAuth challenge is initiated by the auth server's client middleware.
        // The host application configures challenge properties and calls ChallengeAsync()
        // on the authentication scheme corresponding to the provider.
        // This endpoint validates the provider and returns metadata for the frontend
        // to initiate the redirect via the standard OAuth client flow.
        return TypedResults.Ok();
    }

    private static async Task<Results<Ok<ExternalLoginCallbackResponse>, RedirectHttpResult, ProblemHttpResult>> CallbackAsync(
        HttpContext httpContext,
        [FromServices] IExternalLoginService externalLoginService,
        [FromServices] ISettingProvider settingProvider,
        [FromServices] SignInManager<LocalIdentity> signInManager,
        [FromServices] UserManager<LocalIdentity> userManager,
        [FromServices] IDataProtectionProvider dataProtectionProvider,
        [FromServices] IOptions<AccountEndpointsOptions> options,
        [FromQuery] string? mode,
        CancellationToken cancellationToken)
    {
        // Resolve the provider from the external auth ticket — never from the query
        // string. The query string is attacker-controlled: a malicious caller could
        // target the Google OAuth callback URL with ?provider=GitHub and cause the
        // link table to mis-attribute the resulting external login. ASP.NET Core
        // Identity stores the originating scheme in
        // AuthenticationProperties.Items["LoginProvider"] under the well-known
        // IdentityConstants.ExternalScheme cookie.
        ILogger logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Granit.Identity.Local.Endpoints.AccountExternalLoginEndpoints");

        AuthenticateResult authResult = await httpContext
            .AuthenticateAsync(IdentityConstants.ExternalScheme)
            .ConfigureAwait(false);

        if (!authResult.Succeeded
            || authResult.Principal is null
            || authResult.Properties is null
            || !authResult.Properties.Items.TryGetValue("LoginProvider", out string? provider)
            || string.IsNullOrEmpty(provider))
        {
            await TryWriteExternalLoginAuditAsync(httpContext, logger, provider: "unknown",
                userId: null, userName: null, failureReason: "callback_missing_scheme",
                cancellationToken).ConfigureAwait(false);
            return TypedResults.Problem(
                detail: AccountEndpointMessages.Localize(
                    httpContext,
                    "Granit:Identity:ExternalLogin:CallbackMissingScheme",
                    "External login callback did not carry an authentication scheme."),
                statusCode: StatusCodes.Status400BadRequest);
        }

        string? externalUserId = authResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        string? externalUserName = authResult.Principal.Identity?.Name;
        string? returnUrl = authResult.Properties.Items.TryGetValue("returnUrl", out string? carried)
            ? carried : null;

        bool allowRegistration = await IsSelfRegistrationAllowedAsync(settingProvider, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            ProcessCallbackResult result = await externalLoginService
                .ProcessCallbackAsync(authResult.Principal, provider, allowRegistration, cancellationToken)
                .ConfigureAwait(false);

            // Insufficient provider data: no account was created. Mint a short-lived signed token
            // carrying the provider key + tenant, and hand the prefill back so the client can drive
            // the pre-filled registration form. No session is established yet.
            if (result.Status == ProcessCallbackStatus.NewUserNeedsProfile)
            {
                ICurrentTenant? prefillTenant = httpContext.RequestServices.GetService<ICurrentTenant>();
                Guid? tenantId = prefillTenant is { IsAvailable: true } ? prefillTenant.Id : null;
                string token = ExternalRegistrationToken.Protect(
                    dataProtectionProvider,
                    new ExternalRegistrationTokenPayload(
                        result.Prefill!.Provider, result.Prefill.ProviderKey, result.Prefill.Email,
                        result.Prefill.FirstName, result.Prefill.LastName, tenantId));

                return BuildCallbackResult(
                    options.Value, mode, IdentityLocalResponseMapper.ToResponse(result, token), returnUrl);
            }

            // Existing or newly-created account: establish the Identity session so the OIDC flow
            // can resume, mirroring the local login path (cookie on IdentityConstants.ApplicationScheme).
            LocalIdentity? user = await userManager.FindByIdAsync(result.UserId!.Value.ToString())
                .ConfigureAwait(false);
            if (user is not null)
            {
                ICurrentTenant? currentTenant = httpContext.RequestServices.GetService<ICurrentTenant>();
                using (currentTenant?.Change(user.TenantId))
                {
                    await signInManager.SignInAsync(user, isPersistent: false).ConfigureAwait(false);
                }

                // The transient external correlation cookie auto-expires; we deliberately do not
                // SignOut(ExternalScheme) here so the flow does not depend on that scheme being a
                // sign-out-capable handler (it is a cookie in production but may not be elsewhere).
            }

            await TryWriteExternalLoginAuditAsync(httpContext, logger, provider,
                userId: externalUserId, userName: externalUserName, failureReason: null,
                cancellationToken).ConfigureAwait(false);

            return BuildCallbackResult(
                options.Value, mode, IdentityLocalResponseMapper.ToResponse(result, continuationToken: null), returnUrl);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            await TryWriteExternalLoginAuditAsync(httpContext, logger, provider,
                userId: externalUserId, userName: externalUserName,
                failureReason: "account_not_found", cancellationToken).ConfigureAwait(false);
            return TypedResults.Problem(
                detail: AccountEndpointMessages.Localize(
                    httpContext,
                    "Granit:Identity:ExternalLogin:NoLinkedAccount",
                    "No account is linked to this external login."),
                statusCode: StatusCodes.Status403Forbidden);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("DuplicateEmail", StringComparison.OrdinalIgnoreCase))
        {
            await TryWriteExternalLoginAuditAsync(httpContext, logger, provider,
                userId: externalUserId, userName: externalUserName,
                failureReason: "duplicate_email", cancellationToken).ConfigureAwait(false);
            return TypedResults.Problem(
                detail: AccountEndpointMessages.Localize(
                    httpContext,
                    "Granit:Identity:ExternalLogin:EmailAlreadyExists",
                    "An account with this email already exists."),
                statusCode: StatusCodes.Status409Conflict);
        }
    }

    /// <summary>
    /// Records an external-login callback audit row. No-op when
    /// <see cref="IAuditingWriter"/> is not registered; audit failures are
    /// swallowed so the login flow is never blocked by audit issues.
    /// </summary>
    private static async Task TryWriteExternalLoginAuditAsync(
        HttpContext httpContext,
        ILogger logger,
        string provider,
        string? userId,
        string? userName,
        string? failureReason,
        CancellationToken cancellationToken)
    {
        IAuditingWriter? auditingWriter = httpContext.RequestServices.GetService<IAuditingWriter>();
        if (auditingWriter is null)
        {
            return;
        }

        TimeProvider timeProvider = httpContext.RequestServices.GetService<TimeProvider>()
            ?? TimeProvider.System;
        ICurrentTenant? currentTenant = httpContext.RequestServices.GetService<ICurrentTenant>();
        Guid? tenantId = currentTenant is { IsAvailable: true } ? currentTenant.Id : null;
        string? ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        string? userAgent = httpContext.Request.Headers.UserAgent.ToString();
        if (string.IsNullOrEmpty(userAgent))
        {
            userAgent = null;
        }
        string? correlationId = Activity.Current?.Id;
        string method = $"external:{provider.ToLowerInvariant()}";

        string auditUserId = string.IsNullOrEmpty(userId) ? AuthenticationAuditEntry.UnknownUserSentinel : userId;

        AuditEntry entry = failureReason is null
            ? AuthenticationAuditEntry.CreateSuccess(
                timeProvider.GetUtcNow(),
                auditUserId,
                userName, method, tenantId, ipAddress, userAgent, correlationId)
            : AuthenticationAuditEntry.CreateFailure(
                timeProvider.GetUtcNow(), userId, userName, method, failureReason,
                tenantId, ipAddress, userAgent, correlationId);

        try
        {
            await auditingWriter.WriteAsync(entry, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogAuditWriteFailed(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "External login: failed to write authentication audit entry — login flow continues.")]
    private static partial void LogAuditWriteFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "External login: provider '{Provider}' is configured but no authentication handler is registered for it — register its scheme on the host (AddGoogle()/AddMicrosoftAccount()/…).")]
    private static partial void LogProviderSchemeMissing(ILogger logger, string provider);

    private static async Task<Results<NoContent, ProblemHttpResult>> UnlinkExternalLoginAsync(
        string provider,
        HttpContext httpContext,
        [FromServices] IExternalLoginService externalLoginService,
        CancellationToken cancellationToken)
    {
        string userId = httpContext.User.FindFirst("sub")!.Value;

        // Get the provider key for this provider
        IReadOnlyList<ExternalLoginInfo> logins = await externalLoginService
            .GetLoginsAsync(userId, cancellationToken).ConfigureAwait(false);

        ExternalLoginInfo? login = logins.FirstOrDefault(l =>
            l.LoginProvider.Equals(provider, StringComparison.OrdinalIgnoreCase));

        if (login is null)
        {
            return TypedResults.Problem(
                detail: $"No linked login found for provider '{provider}'.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            await externalLoginService
                .RemoveLoginAsync(userId, login.LoginProvider, login.ProviderKey, cancellationToken)
                .ConfigureAwait(false);
            return TypedResults.NoContent();
        }
        catch (InvalidOperationException)
        {
            return TypedResults.Problem(
                detail: AccountEndpointMessages.Localize(
                    httpContext,
                    "Granit:Identity:ExternalLogin:RemoveFailed",
                    "This external login could not be removed."),
                statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>
    /// Initiates the OAuth flow by issuing the actual provider challenge (HTTP 302 to the provider).
    /// The browser navigates here; the provider returns to the callback, which establishes the
    /// session. The optional <c>returnUrl</c> (relative only) is carried through the external ticket
    /// so the callback can resume the original OIDC authorization request.
    /// </summary>
    private static async Task<Results<ChallengeHttpResult, ProblemHttpResult>> StartChallengeAsync(
        string provider,
        [FromQuery] string? returnUrl,
        [FromServices] IExternalProviderRegistry providerRegistry,
        [FromServices] LinkGenerator linkGenerator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        string? schemeName = providerRegistry.GetSchemeName(provider);
        if (schemeName is null
            || !await providerRegistry.IsProviderAvailableAsync(provider, cancellationToken).ConfigureAwait(false))
        {
            return TypedResults.Problem(
                detail: AccountEndpointMessages.Localize(
                    httpContext,
                    "Granit:Identity:ExternalLogin:ProviderNotConfigured",
                    "The specified external login provider is not configured."),
                statusCode: StatusCodes.Status400BadRequest);
        }

        AuthenticationProperties properties = new()
        {
            RedirectUri = linkGenerator.GetPathByName(httpContext, "ExternalLoginCallback"),
        };

        string? safeReturnUrl = SanitizeRelativeUrl(returnUrl);
        if (safeReturnUrl is not null)
        {
            properties.Items["returnUrl"] = safeReturnUrl;
        }

        return TypedResults.Challenge(properties, [schemeName]);
    }

    /// <summary>
    /// Completes registration for an external identity whose provider data was insufficient for a
    /// one-click sign-up. Creates the account from the user-supplied fields, links the external
    /// login carried by the signed token, establishes the session, and publishes
    /// <see cref="UserRegisteredEto"/> (which triggers default-role assignment and the welcome
    /// notification).
    /// </summary>
    private static async Task<Results<Ok<AccountLoginResponse>, ProblemHttpResult>> CompleteRegistrationAsync(
        RegisterExternalRequest request,
        HttpContext httpContext,
        [FromServices] ISettingProvider settingProvider,
        [FromServices] IDataProtectionProvider dataProtectionProvider,
        [FromServices] UserManager<LocalIdentity> userManager,
        [FromServices] SignInManager<LocalIdentity> signInManager,
        [FromServices] IEmailConfirmationService emailConfirmation,
        [FromServices] IDistributedEventBus eventBus,
        [FromServices] IClock clock,
        CancellationToken cancellationToken)
    {
        // Master account-creation gate (per-tenant).
        if (!await IsSelfRegistrationAllowedAsync(settingProvider, cancellationToken).ConfigureAwait(false))
        {
            return TypedResults.Problem(
                detail: AccountEndpointMessages.Localize(
                    httpContext, "Granit:Identity:Account:SelfRegistrationDisabled", "Self-registration is disabled."),
                statusCode: StatusCodes.Status403Forbidden);
        }

        ExternalRegistrationTokenPayload? payload =
            ExternalRegistrationToken.TryUnprotect(dataProtectionProvider, request.Token);
        if (payload is null)
        {
            return TypedResults.Problem(
                detail: AccountEndpointMessages.Localize(
                    httpContext, "Granit:Identity:ExternalRegistration:InvalidToken",
                    "The registration session is invalid or has expired. Please restart the sign-in."),
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Cross-tenant guard: a token minted in tenant A must not be redeemed in tenant B (or host).
        ICurrentTenant? currentTenant = httpContext.RequestServices.GetService<ICurrentTenant>();
        Guid? currentTenantId = currentTenant is { IsAvailable: true } ? currentTenant.Id : null;
        if (payload.TenantId != currentTenantId)
        {
            return TypedResults.Problem(
                detail: AccountEndpointMessages.Localize(
                    httpContext, "Granit:Identity:ExternalRegistration:TenantMismatch",
                    "The registration session does not match the current tenant."),
                statusCode: StatusCodes.Status403Forbidden);
        }

        // When the provider supplied a verified email, it is authoritative — the user cannot swap it.
        // When it did not, the user-entered email is unverified and must be confirmed by email.
        bool emailVerified;
        string email;
        if (!string.IsNullOrWhiteSpace(payload.Email))
        {
            if (!string.Equals(payload.Email, request.Email, StringComparison.OrdinalIgnoreCase))
            {
                return TypedResults.Problem(
                    detail: AccountEndpointMessages.Localize(
                        httpContext, "Granit:Identity:ExternalRegistration:EmailMismatch",
                        "The email does not match the one verified by the provider."),
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            email = payload.Email;
            emailVerified = true;
        }
        else
        {
            email = request.Email;
            emailVerified = false;
        }

        LocalIdentity newUser = new()
        {
            UserName = email,
            Email = email,
            FirstName = request.FirstName ?? payload.FirstName,
            LastName = request.LastName ?? payload.LastName,
            EmailConfirmed = emailVerified,
            // Durable registration-event intent, cleared after the inline publish below (see the
            // reconciler for the crash-recovery path).
            RegistrationEventPendingSince = clock.Now,
        };

        IdentityResult createResult = await userManager.CreateAsync(newUser).ConfigureAwait(false);
        if (!createResult.Succeeded)
        {
            bool duplicate = createResult.Errors.Any(e =>
                e.Code.Contains("Duplicate", StringComparison.OrdinalIgnoreCase));
            return duplicate
                ? TypedResults.Problem(
                    detail: AccountEndpointMessages.Localize(
                        httpContext, "Granit:Identity:ExternalLogin:EmailAlreadyExists",
                        "An account with this email already exists."),
                    statusCode: StatusCodes.Status409Conflict)
                : TypedResults.Problem(
                    detail: string.Join(", ", createResult.Errors.Select(e => e.Description)),
                    statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        IdentityResult linkResult = await userManager.AddLoginAsync(
            newUser, new UserLoginInfo(payload.Provider, payload.ProviderKey, payload.Provider))
            .ConfigureAwait(false);
        if (!linkResult.Succeeded)
        {
            // Roll back the just-created account so a failed link never leaves an orphan.
            await userManager.DeleteAsync(newUser).ConfigureAwait(false);
            return TypedResults.Problem(
                detail: string.Join(", ", linkResult.Errors.Select(e => e.Description)),
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        if (!emailVerified)
        {
            await emailConfirmation.SendConfirmationEmailAsync(newUser.Id.ToString(), email, cancellationToken)
                .ConfigureAwait(false);
        }

        using (currentTenant?.Change(newUser.TenantId))
        {
            await signInManager.SignInAsync(newUser, isPersistent: false).ConfigureAwait(false);
        }

        await eventBus.PublishAsync(
            new UserRegisteredEto(newUser.Id, newUser.TenantId), cancellationToken).ConfigureAwait(false);

        newUser.RegistrationEventPendingSince = null;
        await userManager.UpdateAsync(newUser).ConfigureAwait(false);

        return TypedResults.Ok(new AccountLoginResponse(Succeeded: true));
    }

    private static async Task<bool> IsSelfRegistrationAllowedAsync(
        ISettingProvider settingProvider, CancellationToken cancellationToken)
    {
        string? allowed = await settingProvider
            .GetOrNullAsync(IdentityLocalSettingNames.AllowSelfRegistration, cancellationToken)
            .ConfigureAwait(false);
        return string.Equals(allowed, "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Builds the callback result: a JSON <see cref="ExternalLoginCallbackResponse"/> for headless
    /// hosts (or when <c>?mode=json</c>), or a 302 redirect to the configured frontend URL carrying
    /// the status and continuation data otherwise.
    /// </summary>
    private static Results<Ok<ExternalLoginCallbackResponse>, RedirectHttpResult, ProblemHttpResult> BuildCallbackResult(
        AccountEndpointsOptions options,
        string? mode,
        ExternalLoginCallbackResponse response,
        string? returnUrl)
    {
        bool forceJson = string.Equals(mode, "json", StringComparison.OrdinalIgnoreCase);
        if (forceJson || string.IsNullOrWhiteSpace(options.ExternalLoginCallbackRedirectUrl))
        {
            return TypedResults.Ok(response);
        }

        Dictionary<string, string?> query = new() { ["status"] = response.Status };
        if (response.Status == ExternalLoginCallbackResponse.StatusNeedsProfileCompletion)
        {
            query["token"] = response.ContinuationToken;
            if (response.Prefill?.Email is { } prefillEmail)
            {
                query["email"] = prefillEmail;
            }

            if (response.Prefill?.FirstName is { } prefillFirstName)
            {
                query["firstName"] = prefillFirstName;
            }

            if (response.Prefill?.LastName is { } prefillLastName)
            {
                query["lastName"] = prefillLastName;
            }
        }
        else if (returnUrl is not null)
        {
            query["returnUrl"] = returnUrl;
        }

        string redirectUrl = QueryHelpers.AddQueryString(options.ExternalLoginCallbackRedirectUrl!, query);
        return TypedResults.Redirect(redirectUrl);
    }

    /// <summary>
    /// Returns the URL only if it is a safe site-relative path (single leading slash, no scheme or
    /// authority), guarding against open redirects. Otherwise <see langword="null"/>.
    /// </summary>
    private static string? SanitizeRelativeUrl(string? url) =>
        !string.IsNullOrEmpty(url)
            && url.StartsWith('/')
            && !url.StartsWith("//", StringComparison.Ordinal)
            && !url.StartsWith("/\\", StringComparison.Ordinal)
            ? url
            : null;
}
