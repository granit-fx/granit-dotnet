using System.Collections.ObjectModel;
using Granit.Identity;

namespace Granit.Tests.Shared;

/// <summary>
/// Lightweight <see cref="IIdentityUser"/> for unit tests.
/// Avoids depending on <c>Granit.Identity.Federated</c> in non-federated test projects.
/// </summary>
internal sealed record FakeIdentityUser(
    string UserId,
    string? Username,
    string? Email,
    string? FirstName,
    string? LastName,
    bool Enabled) : IIdentityUser
{
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        ReadOnlyDictionary<string, string>.Empty;
}
