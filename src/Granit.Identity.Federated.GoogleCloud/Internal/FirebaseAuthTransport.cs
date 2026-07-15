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
    public async Task<IReadOnlyList<ExportedUserRecord>> ListUsersAsync(
        int? skip = null,
        int? take = null,
        CancellationToken cancellationToken = default)
    {
        int skipCount = skip ?? 0;
        int takeCount = take ?? DefaultPageSize;
        int targetCount = skipCount + takeCount;

        // Page size is bounded by Identity Toolkit (max 1000). Pick the smaller
        // of the requested window and 1000 so we don't over-fetch on tiny windows.
        int pageSize = Math.Min(MaxIdentityToolkitPageSize, Math.Max(1, targetCount));
        ListUsersOptions options = new() { PageSize = pageSize };
        PagedAsyncEnumerable<ExportedUserRecords, ExportedUserRecord> pagedEnumerable = auth.ListUsersAsync(options);

        List<ExportedUserRecord> users = new(takeCount);
        int seen = 0;

        IAsyncEnumerator<ExportedUserRecord> enumerator = pagedEnumerable.GetAsyncEnumerator(cancellationToken);

        try
        {
            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                if (seen++ < skipCount)
                {
                    continue;
                }

                users.Add(enumerator.Current);

                if (users.Count >= takeCount)
                {
                    break;
                }
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }

        return users;
    }

    private const int DefaultPageSize = 100;
    private const int MaxIdentityToolkitPageSize = 1000;

    /// <inheritdoc />
    public async Task PingAsync(CancellationToken cancellationToken = default)
    {
        ListUsersOptions options = new() { PageSize = 1 };
        PagedAsyncEnumerable<ExportedUserRecords, ExportedUserRecord> pagedEnumerable = auth.ListUsersAsync(options);

        IAsyncEnumerator<ExportedUserRecord> enumerator = pagedEnumerable.GetAsyncEnumerator(cancellationToken);
        try
        {
            // Trigger the first network call. We don't care whether the project has users —
            // a successful round-trip (even with an empty page) confirms the service account
            // can authenticate and reach Identity Toolkit.
            await enumerator.MoveNextAsync().ConfigureAwait(false);
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }
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
}
