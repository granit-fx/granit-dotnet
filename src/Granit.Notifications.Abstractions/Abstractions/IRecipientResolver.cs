namespace Granit.Notifications.Abstractions;

/// <summary>
/// Resolves recipient contact information from a user identifier.
/// Must be implemented by the application (not provided by Granit).
/// </summary>
public interface IRecipientResolver
{
    /// <summary>
    /// Resolves contact information for the given user.
    /// Returns <c>null</c> if the user cannot be found.
    /// </summary>
    Task<RecipientInfo?> ResolveAsync(string userId, CancellationToken cancellationToken = default);
}
