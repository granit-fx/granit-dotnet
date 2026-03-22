using System.Diagnostics.CodeAnalysis;
using FirebaseAdmin.Auth;
using Google.Api.Gax;

namespace Granit.Identity.Federated.GoogleCloud.Internal;

/// <summary>
/// Production transport wrapping the Firebase Admin SDK.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class FirebaseAuthTransport(FirebaseAuth auth) : IFirebaseAuthTransport
{
    /// <inheritdoc />
    public Task<UserRecord> GetUserAsync(string uid, CancellationToken cancellationToken = default) =>
        auth.GetUserAsync(uid, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExportedUserRecord>> ListUsersAsync(CancellationToken cancellationToken = default)
    {
        ListUsersOptions options = new() { PageSize = 1000 };
        PagedAsyncEnumerable<ExportedUserRecords, ExportedUserRecord> pagedEnumerable = auth.ListUsersAsync(options);

        List<ExportedUserRecord> users = [];
        IAsyncEnumerator<ExportedUserRecord> enumerator = pagedEnumerable.GetAsyncEnumerator(cancellationToken);

        try
        {
            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                users.Add(enumerator.Current);
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }

        return users;
    }

    /// <inheritdoc />
    public Task<UserRecord> CreateUserAsync(UserRecordArgs args, CancellationToken cancellationToken = default) =>
        auth.CreateUserAsync(args, cancellationToken);

    /// <inheritdoc />
    public Task UpdateUserAsync(UserRecordArgs args, CancellationToken cancellationToken = default) =>
        auth.UpdateUserAsync(args, cancellationToken);

    /// <inheritdoc />
    public Task SetCustomUserClaimsAsync(string uid, IReadOnlyDictionary<string, object> claims, CancellationToken cancellationToken = default) =>
        auth.SetCustomUserClaimsAsync(uid, claims, cancellationToken);

    /// <inheritdoc />
    public Task RevokeRefreshTokensAsync(string uid, CancellationToken cancellationToken = default) =>
        auth.RevokeRefreshTokensAsync(uid, cancellationToken);

    /// <inheritdoc />
    public Task<string> GeneratePasswordResetLinkAsync(string email, CancellationToken cancellationToken = default) =>
        auth.GeneratePasswordResetLinkAsync(email);

    /// <inheritdoc />
    public Task<bool> VerifyPasswordAsync(string email, string password, CancellationToken cancellationToken = default) =>
        // Firebase Admin SDK does not expose a direct password verify API.
        // This would require the Firebase Auth REST API with the Web API key.
        // For now, this capability is not supported in the admin SDK.
        Task.FromResult(false);
}
