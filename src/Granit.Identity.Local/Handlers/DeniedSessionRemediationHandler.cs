using System.Diagnostics.CodeAnalysis;
using Granit.Identity.Local.Services;
using Granit.MultiTenancy;
using Microsoft.Extensions.Logging;

namespace Granit.Identity.Local.Handlers;

/// <summary>
/// Forces a credential reset when a user answers "no, it wasn't me" to a suspicious-session alert. Subscribes to
/// <see cref="SessionDeniedEto"/> (the sessions are already revoked by the review endpoint) and triggers a local
/// password reset for the affected account.
/// </summary>
/// <remarks>
/// Local-only by design: this handler ships with <c>Granit.Identity.Local</c>, so a federated-only deployment
/// (where the upstream IdP owns credentials) simply has no subscriber and the reset is skipped. Non-blocking and
/// idempotent — an unknown or non-local subject is logged and skipped; <see cref="IPasswordResetService"/>
/// already treats a missing user as a no-op, so a duplicate delivery just re-issues a reset for the same email.
/// </remarks>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public partial class DeniedSessionRemediationHandler
{
    public static async Task HandleAsync(
        SessionDeniedEto evt,
        IIdentityUserReader userReader,
        IPasswordResetService passwordResetService,
        ICurrentTenant currentTenant,
        ILogger<DeniedSessionRemediationHandler> logger,
        CancellationToken cancellationToken)
    {
        // Resolve and reset within the user's tenant scope (the distributed dispatch carries no ambient tenant).
        using IDisposable? tenantScope = currentTenant.Change(evt.TenantId);

        IIdentityUser? user = await userReader.GetUserAsync(evt.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null || string.IsNullOrEmpty(user.Email))
        {
            // Not a local account (federated subject) or no email on file — nothing this handler can reset.
            LogResetSkipped(logger, evt.UserId);
            return;
        }

        await passwordResetService.RequestResetAsync(user.Email, cancellationToken).ConfigureAwait(false);
        LogResetRequested(logger, evt.UserId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Forced a password reset for user {UserId} after a denied-session review.")]
    private static partial void LogResetRequested(ILogger logger, string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Denied-session review for user {UserId} required no local credential reset (not a local account or no email).")]
    private static partial void LogResetSkipped(ILogger logger, string userId);
}
