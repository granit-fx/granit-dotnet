using Granit.Authorization;
using Granit.Mentions.Internal;
using NSubstitute;
using Shouldly;

namespace Granit.Mentions.Tests;

public sealed class PermissionMentionAuthorizerTests
{
    private sealed class StubResolver(string? requiredPermission) : IMentionResolver
    {
        public string Type => "stub";
        public string? RequiredPermission => requiredPermission;

        public ValueTask<IReadOnlyList<MentionSuggestion>> SearchAsync(string query, int limit, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<MentionSuggestion>>([]);

        public ValueTask<MentionTarget?> ResolveAsync(string id, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<MentionTarget?>(null);
    }

    private readonly IPermissionChecker _checker = Substitute.For<IPermissionChecker>();

    [Fact]
    public async Task A_resolver_without_a_required_permission_is_always_authorized()
    {
        bool allowed = await new PermissionMentionAuthorizer(_checker)
            .IsAuthorizedAsync(new StubResolver(null), TestContext.Current.CancellationToken);

        allowed.ShouldBeTrue();
        await _checker.DidNotReceive().IsGrantedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_gated_resolver_follows_the_permission_grant()
    {
        _checker.IsGrantedAsync("x.read", Arg.Any<CancellationToken>()).Returns(true);

        bool allowed = await new PermissionMentionAuthorizer(_checker)
            .IsAuthorizedAsync(new StubResolver("x.read"), TestContext.Current.CancellationToken);

        allowed.ShouldBeTrue();
    }

    [Fact]
    public async Task A_gated_resolver_is_denied_when_not_granted()
    {
        _checker.IsGrantedAsync("x.read", Arg.Any<CancellationToken>()).Returns(false);

        bool allowed = await new PermissionMentionAuthorizer(_checker)
            .IsAuthorizedAsync(new StubResolver("x.read"), TestContext.Current.CancellationToken);

        allowed.ShouldBeFalse();
    }
}
