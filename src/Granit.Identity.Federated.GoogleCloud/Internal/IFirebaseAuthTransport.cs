using FirebaseAdmin.Auth;

namespace Granit.Identity.Federated.GoogleCloud.Internal;

/// <summary>
/// Abstraction over <see cref="FirebaseAuth"/> for testability.
/// </summary>
internal interface IFirebaseAuthTransport
{
    Task<UserRecord> GetUserAsync(string uid, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExportedUserRecord>> ListUsersAsync(CancellationToken cancellationToken = default);

    Task<UserRecord> CreateUserAsync(UserRecordArgs args, CancellationToken cancellationToken = default);

    Task UpdateUserAsync(UserRecordArgs args, CancellationToken cancellationToken = default);

    Task SetCustomUserClaimsAsync(string uid, IReadOnlyDictionary<string, object> claims, CancellationToken cancellationToken = default);

    Task RevokeRefreshTokensAsync(string uid, CancellationToken cancellationToken = default);

    Task<string> GeneratePasswordResetLinkAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> VerifyPasswordAsync(string email, string password, CancellationToken cancellationToken = default);
}
