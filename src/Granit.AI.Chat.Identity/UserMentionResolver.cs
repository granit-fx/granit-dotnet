using Granit.AI.Chat.Mentions;
using Granit.Identity;
using Granit.Identity.Endpoints.Permissions;

namespace Granit.AI.Chat.Identity;

/// <summary>
/// Resolves <c>@user</c> mentions against the active identity provider (local or federated) via
/// <see cref="IIdentityUserReader"/>. Both search and resolve run per scope under the caller's
/// identity, so a directory the caller cannot read surfaces nothing (ADR-067).
/// </summary>
internal sealed class UserMentionResolver(IIdentityUserReader users) : IAIMentionResolver
{
    /// <summary>The mention type. Apps referencing users elsewhere should reuse this literal.</summary>
    public const string MentionType = "user";

    /// <summary>
    /// The same gate as <c>GET /identity/users</c> (<see cref="IdentityPermissions.Users.Read"/>):
    /// a caller who may chat but may not read the directory sees no <c>@user</c> suggestions.
    /// </summary>
    public const string ReadUsersPermission = IdentityPermissions.Users.Read;

    public string Type => MentionType;

    public string? RequiredPermission => ReadUsersPermission;

    public async ValueTask<AIMentionContext?> ResolveAsync(string id, CancellationToken cancellationToken = default)
    {
        IIdentityUser? user = await users.GetUserAsync(id, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return null;
        }

        return new AIMentionContext
        {
            Type = MentionType,
            Id = user.UserId,
            Label = DisplayName(user),
            Content = BuildContent(user),
        };
    }

    public async ValueTask<IReadOnlyList<AIMentionSuggestion>> SearchAsync(
        string query, int limit, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IIdentityUser> matches = await users
            .GetUsersAsync(string.IsNullOrWhiteSpace(query) ? null : query, first: 0, max: limit, cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. matches.Select(user => new AIMentionSuggestion
            {
                Type = MentionType,
                Id = user.UserId,
                Label = DisplayName(user),
                Description = user.Email,
            }),
        ];
    }

    private static string DisplayName(IIdentityUser user)
    {
        string fullName = $"{user.FirstName} {user.LastName}".Trim();
        return !string.IsNullOrEmpty(fullName)
            ? fullName
            : user.Username ?? user.Email ?? user.UserId;
    }

    private static string BuildContent(IIdentityUser user)
    {
        // A compact, label-free body; the framework prepends "user: {Label}" and the untrusted envelope.
        List<string> lines = [];
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            lines.Add($"Email: {user.Email}");
        }

        if (!string.IsNullOrWhiteSpace(user.Username))
        {
            lines.Add($"Username: {user.Username}");
        }

        lines.Add($"Enabled: {user.Enabled}");
        return string.Join('\n', lines);
    }
}
