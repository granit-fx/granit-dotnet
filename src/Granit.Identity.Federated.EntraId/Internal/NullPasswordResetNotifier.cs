using Microsoft.Extensions.Logging;

namespace Granit.Identity.Federated.EntraId.Internal;

/// <summary>
/// Null-object implementation of <see cref="IPasswordResetNotifier"/>
/// that logs a warning when invoked.
/// </summary>
internal sealed partial class NullPasswordResetNotifier(
    ILogger<NullPasswordResetNotifier> logger) : IPasswordResetNotifier
{
    /// <inheritdoc/>
    public Task NotifyAsync(string userId, string temporaryPassword, CancellationToken cancellationToken = default)
    {
        LogPasswordResetNotifierNotRegistered(userId);
        return Task.CompletedTask;
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "No IPasswordResetNotifier registered. Temporary password set for user {UserId} but no notification was sent. " +
                  "Register an IPasswordResetNotifier implementation to send password reset notifications")]
    private partial void LogPasswordResetNotifierNotRegistered(string userId);
}
