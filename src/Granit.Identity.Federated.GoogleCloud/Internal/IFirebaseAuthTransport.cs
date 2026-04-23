using FirebaseAdmin.Auth;

namespace Granit.Identity.Federated.GoogleCloud.Internal;

/// <summary>
/// Abstraction over <see cref="FirebaseAuth"/> for testability.
/// </summary>
internal interface IFirebaseAuthTransport
{
    Task<UserRecord> GetUserAsync(string uid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists a bounded slice of users from Firebase Auth, iterating page-by-page
    /// over the Identity Toolkit cursor and stopping after the requested window.
    /// </summary>
    /// <param name="skip">Number of records to skip from the start of the directory. Defaults to 0.</param>
    /// <param name="take">Maximum number of records to materialise. Defaults to 100.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// Bounding the slice avoids materialising large directories (100k+ users) into
    /// memory on every list call and limits the Identity Toolkit quota footprint —
    /// see VULN-204 in the Identity audit.
    /// </remarks>
    Task<IReadOnlyList<ExportedUserRecord>> ListUsersAsync(
        int? skip = null,
        int? take = null,
        CancellationToken cancellationToken = default);

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
