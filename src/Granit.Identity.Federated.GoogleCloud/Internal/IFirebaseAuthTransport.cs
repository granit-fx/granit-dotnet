using FirebaseAdmin.Auth;

namespace Granit.Identity.Federated.GoogleCloud.Internal;

/// <summary>
/// Abstraction over <see cref="FirebaseAuth"/> for testability.
/// </summary>
internal interface IFirebaseAuthTransport
{
    Task<UserRecord> GetUserAsync(string uid, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExportedUserRecord>> ListUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues a minimal Identity Toolkit call to verify connectivity and credentials.
    /// </summary>
    /// <remarks>
    /// Reads the first page of users with <c>PageSize = 1</c>. Empty projects return
    /// an empty page (still a successful round-trip).
    /// </remarks>
    Task PingAsync(CancellationToken cancellationToken = default);

    Task<UserRecord> CreateUserAsync(UserRecordArgs args, CancellationToken cancellationToken = default);

    Task UpdateUserAsync(UserRecordArgs args, CancellationToken cancellationToken = default);

    Task SetCustomUserClaimsAsync(string uid, IReadOnlyDictionary<string, object> claims, CancellationToken cancellationToken = default);

    Task RevokeRefreshTokensAsync(string uid, CancellationToken cancellationToken = default);

    Task<string> GeneratePasswordResetLinkAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> VerifyPasswordAsync(string email, string password, CancellationToken cancellationToken = default);
}
