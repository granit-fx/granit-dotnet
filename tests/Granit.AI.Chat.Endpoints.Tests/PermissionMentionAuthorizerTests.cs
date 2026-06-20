using Granit.AI.Chat.Endpoints.Internal;
using Granit.AI.Chat.Mentions;
using Granit.Authorization;
using NSubstitute;
using Shouldly;

namespace Granit.AI.Chat.Endpoints.Tests;

public sealed class PermissionMentionAuthorizerTests
{
    private sealed class StubResolver(string type, string? requiredPermission) : IAIMentionResolver
    {
        public string Type => type;
        public string? RequiredPermission => requiredPermission;

        public ValueTask<AIMentionContext?> ResolveAsync(string id, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<AIMentionContext?>(null);

        public ValueTask<IReadOnlyList<AIMentionSuggestion>> SearchAsync(
            string query, int limit, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<AIMentionSuggestion>>([]);
    }

    private readonly IPermissionChecker _checker = Substitute.For<IPermissionChecker>();

    [Fact]
    public async Task A_resolver_without_a_required_permission_is_always_authorized()
    {
        var authorizer = new PermissionMentionAuthorizer(_checker);

        bool allowed = await authorizer.IsAuthorizedAsync(
            new StubResolver("note", null), TestContext.Current.CancellationToken);

        allowed.ShouldBeTrue();
        // No permission to check, so the checker is never consulted.
        await _checker.DidNotReceive().IsGrantedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_gated_resolver_is_authorized_when_the_permission_is_granted()
    {
        _checker.IsGrantedAsync("Identity.Users.Read", Arg.Any<CancellationToken>()).Returns(true);
        var authorizer = new PermissionMentionAuthorizer(_checker);

        bool allowed = await authorizer.IsAuthorizedAsync(
            new StubResolver("user", "Identity.Users.Read"), TestContext.Current.CancellationToken);

        allowed.ShouldBeTrue();
    }

    [Fact]
    public async Task A_gated_resolver_is_denied_when_the_permission_is_not_granted()
    {
        _checker.IsGrantedAsync("Identity.Users.Read", Arg.Any<CancellationToken>()).Returns(false);
        var authorizer = new PermissionMentionAuthorizer(_checker);

        bool allowed = await authorizer.IsAuthorizedAsync(
            new StubResolver("user", "Identity.Users.Read"), TestContext.Current.CancellationToken);

        allowed.ShouldBeFalse();
    }
}
