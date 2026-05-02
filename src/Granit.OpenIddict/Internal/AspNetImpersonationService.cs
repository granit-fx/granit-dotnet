using System.Collections.Immutable;
using System.Security.Claims;
using Granit.Events;
using Granit.Identity.Local.Diagnostics;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Events;
using Granit.Identity.Local.Extensions;
using Granit.Identity.Local.Services;
using Granit.Timing;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;

#pragma warning disable EF1001 // OpenIddictDbContext is internal but accessible via InternalsVisibleTo
#pragma warning disable GRSEC003 // Token/claim constants, not secrets

namespace Granit.OpenIddict.Internal;

/// <summary>
/// <see cref="IImpersonationService"/> implementation that issues OpenIddict tokens
/// with impersonator claims for administrator impersonation.
/// </summary>
internal sealed partial class AspNetImpersonationService(
    UserManager<LocalIdentity> userManager,
    IOpenIddictTokenManager tokenManager,
    IDistributedEventBus eventBus,
    IdentityLocalMetrics metrics,
    IClock clock,
    ILogger<AspNetImpersonationService> logger) : IImpersonationService
{
    private static readonly TimeSpan ImpersonationTokenLifetime = TimeSpan.FromHours(1);

    /// <inheritdoc/>
    public async Task<ImpersonationResult> ImpersonateAsync(
        string targetUserId,
        string impersonatorId,
        string impersonatorName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(impersonatorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(impersonatorName);

        LocalIdentity targetUser = await userManager.FindByIdAsync(targetUserId).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Target user '{targetUserId}' not found.");

        // Build claims principal for the impersonated user
        var identity = new ClaimsIdentity(
            OpenIddictConstants.Schemes.Bearer,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);

        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Subject, targetUser.Id.ToString()));
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Name, targetUser.UserName ?? string.Empty));
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Email, targetUser.Email ?? string.Empty));

        // Add impersonator claims
        identity.AddClaim(new Claim(ClaimsPrincipalExtensions.ImpersonatorIdClaimType, impersonatorId));
        identity.AddClaim(new Claim(ClaimsPrincipalExtensions.ImpersonatorNameClaimType, impersonatorName));

        // Add user roles
        System.Collections.Generic.IList<string> roles = await userManager.GetRolesAsync(targetUser).ConfigureAwait(false);
        foreach (string role in roles)
        {
            identity.AddClaim(new Claim(OpenIddictConstants.Claims.Role, role));
        }

        var principal = new ClaimsPrincipal(identity);

        // Set scopes and destinations
        principal.SetScopes(
            OpenIddictConstants.Scopes.OpenId,
            OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Scopes.Email,
            OpenIddictConstants.Scopes.Roles);

        // Set destinations for all claims
        foreach (Claim claim in principal.Claims)
        {
            claim.SetDestinations(GetImpersonationDestinations(claim));
        }

        // Create access token descriptor
        DateTimeOffset now = clock.Now;
        var accessTokenDescriptor = new OpenIddictTokenDescriptor
        {
            Principal = principal,
            Subject = targetUser.Id.ToString(),
            CreationDate = now,
            ExpirationDate = now + ImpersonationTokenLifetime,
            Type = OpenIddictConstants.TokenTypeHints.AccessToken,
        };

        // Create tokens via OpenIddict manager
        object accessTokenEntry = await tokenManager.CreateAsync(accessTokenDescriptor, cancellationToken).ConfigureAwait(false);
        string? accessToken = await tokenManager.GetPayloadAsync(accessTokenEntry, cancellationToken).ConfigureAwait(false);

        var refreshTokenDescriptor = new OpenIddictTokenDescriptor
        {
            Principal = principal,
            Subject = targetUser.Id.ToString(),
            CreationDate = now,
            ExpirationDate = now + ImpersonationTokenLifetime,
            Type = OpenIddictConstants.TokenTypeHints.RefreshToken,
        };

        object refreshTokenEntry = await tokenManager.CreateAsync(refreshTokenDescriptor, cancellationToken).ConfigureAwait(false);
        string? refreshToken = await tokenManager.GetPayloadAsync(refreshTokenEntry, cancellationToken).ConfigureAwait(false);

        // Publish integration event for transparency notification
        await eventBus.PublishAsync(
            new UserImpersonatedEto(
                Guid.Parse(targetUserId),
                Guid.Parse(impersonatorId),
                targetUser.TenantId,
                now),
            cancellationToken).ConfigureAwait(false);

        metrics.RecordImpersonation(targetUser.TenantId?.ToString());
        Log.ImpersonationStarted(logger, impersonatorId, targetUserId);

        return new ImpersonationResult(
            accessToken ?? string.Empty,
            refreshToken ?? string.Empty,
            (int)ImpersonationTokenLifetime.TotalSeconds);
    }

    /// <inheritdoc/>
    public async Task<ImpersonationResult> BackToImpersonatorAsync(
        string impersonatorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(impersonatorId);

        LocalIdentity adminUser = await userManager.FindByIdAsync(impersonatorId).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Impersonator user '{impersonatorId}' not found.");

        // Build a fresh claims principal for the admin (no impersonator claims)
        var identity = new ClaimsIdentity(
            OpenIddictConstants.Schemes.Bearer,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);

        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Subject, adminUser.Id.ToString()));
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Name, adminUser.UserName ?? string.Empty));
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Email, adminUser.Email ?? string.Empty));

        System.Collections.Generic.IList<string> roles = await userManager.GetRolesAsync(adminUser).ConfigureAwait(false);
        foreach (string role in roles)
        {
            identity.AddClaim(new Claim(OpenIddictConstants.Claims.Role, role));
        }

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(
            OpenIddictConstants.Scopes.OpenId,
            OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Scopes.Email,
            OpenIddictConstants.Scopes.Roles);

        foreach (Claim claim in principal.Claims)
        {
            claim.SetDestinations(GetStandardDestinations(claim));
        }

        DateTimeOffset now = clock.Now;
        var accessTokenDescriptor = new OpenIddictTokenDescriptor
        {
            Principal = principal,
            Subject = adminUser.Id.ToString(),
            CreationDate = now,
            ExpirationDate = now + TimeSpan.FromHours(1),
            Type = OpenIddictConstants.TokenTypeHints.AccessToken,
        };

        object accessTokenEntry = await tokenManager.CreateAsync(accessTokenDescriptor, cancellationToken).ConfigureAwait(false);
        string? accessToken = await tokenManager.GetPayloadAsync(accessTokenEntry, cancellationToken).ConfigureAwait(false);

        var refreshTokenDescriptor = new OpenIddictTokenDescriptor
        {
            Principal = principal,
            Subject = adminUser.Id.ToString(),
            CreationDate = now,
            ExpirationDate = now + TimeSpan.FromDays(14),
            Type = OpenIddictConstants.TokenTypeHints.RefreshToken,
        };

        object refreshTokenEntry = await tokenManager.CreateAsync(refreshTokenDescriptor, cancellationToken).ConfigureAwait(false);
        string? refreshToken = await tokenManager.GetPayloadAsync(refreshTokenEntry, cancellationToken).ConfigureAwait(false);

        Log.ImpersonationEnded(logger, impersonatorId);

        return new ImpersonationResult(
            accessToken ?? string.Empty,
            refreshToken ?? string.Empty,
            3600);
    }

    private static ImmutableArray<string> GetImpersonationDestinations(Claim claim)
    {
        // All impersonation claims go to both access_token and id_token
        return claim.Type switch
        {
            OpenIddictConstants.Claims.Subject or
            OpenIddictConstants.Claims.Name or
            OpenIddictConstants.Claims.Email or
            OpenIddictConstants.Claims.Role or
            ClaimsPrincipalExtensions.ImpersonatorIdClaimType or
            ClaimsPrincipalExtensions.ImpersonatorNameClaimType =>
            [
                OpenIddictConstants.Destinations.AccessToken,
                OpenIddictConstants.Destinations.IdentityToken,
            ],
            _ => [OpenIddictConstants.Destinations.AccessToken],
        };
    }

    private static ImmutableArray<string> GetStandardDestinations(Claim claim)
    {
        return claim.Type switch
        {
            OpenIddictConstants.Claims.Subject or
            OpenIddictConstants.Claims.Name or
            OpenIddictConstants.Claims.Email =>
            [
                OpenIddictConstants.Destinations.AccessToken,
                OpenIddictConstants.Destinations.IdentityToken,
            ],
            _ => [OpenIddictConstants.Destinations.AccessToken],
        };
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information,
            Message = "Impersonation started: admin {ImpersonatorId} → user {TargetUserId}")]
        public static partial void ImpersonationStarted(ILogger logger, string impersonatorId, string targetUserId);

        [LoggerMessage(Level = LogLevel.Information,
            Message = "Impersonation ended: admin {ImpersonatorId} returned to own session")]
        public static partial void ImpersonationEnded(ILogger logger, string impersonatorId);
    }
}
