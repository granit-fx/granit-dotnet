using Granit.Mentions;
using NSubstitute;
using Shouldly;

namespace Granit.Identity.Mentions.Tests;

public sealed class UserMentionResolverTests
{
    private sealed record FakeUser(
        string UserId,
        string? Username = null,
        string? Email = null,
        string? FirstName = null,
        string? LastName = null,
        bool Enabled = true) : IIdentityUser
    {
        public IReadOnlyDictionary<string, string> Metadata { get; } = new Dictionary<string, string>();
    }

    private readonly IIdentityUserReader _users = Substitute.For<IIdentityUserReader>();

    private UserMentionResolver Build() => new(_users);

    [Fact]
    public void Type_is_user()
    {
        Build().Type.ShouldBe("user");
    }

    [Fact]
    public void Required_permission_matches_the_canonical_identity_users_read_constant()
    {
        Build().RequiredPermission.ShouldBe(Granit.Identity.Endpoints.Permissions.IdentityPermissions.Users.Read);
    }

    [Fact]
    public async Task Resolve_returns_null_when_the_user_is_absent()
    {
        _users.GetUserAsync("ghost", Arg.Any<CancellationToken>()).Returns((IIdentityUser?)null);

        MentionTarget? target = await Build().ResolveAsync("ghost", TestContext.Current.CancellationToken);

        target.ShouldBeNull();
    }

    [Fact]
    public async Task Resolve_maps_a_found_user_to_a_target()
    {
        _users.GetUserAsync("u1", Arg.Any<CancellationToken>())
            .Returns(new FakeUser("u1", Username: "ada", Email: "ada@x.io", FirstName: "Ada", LastName: "Lovelace"));

        MentionTarget? target = await Build().ResolveAsync("u1", TestContext.Current.CancellationToken);

        target.ShouldNotBeNull();
        target.Type.ShouldBe("user");
        target.Id.ShouldBe("u1");
        target.Label.ShouldBe("Ada Lovelace");
        target.Content.ShouldContain("ada@x.io");
        target.Content.ShouldContain("Username: ada");
    }

    [Fact]
    public async Task Search_maps_users_to_suggestions_with_email_as_description()
    {
        _users.GetUsersAsync("ada", 0, 8, Arg.Any<CancellationToken>())
            .Returns([new FakeUser("u1", Username: "ada", Email: "ada@x.io", FirstName: "Ada", LastName: "Lovelace")]);

        IReadOnlyList<MentionSuggestion> results =
            await Build().SearchAsync("ada", 8, TestContext.Current.CancellationToken);

        results.ShouldHaveSingleItem();
        results[0].Type.ShouldBe("user");
        results[0].Id.ShouldBe("u1");
        results[0].Label.ShouldBe("Ada Lovelace");
        results[0].Description.ShouldBe("ada@x.io");
    }

    [Fact]
    public async Task Search_falls_back_to_username_then_email_then_id_for_the_label()
    {
        _users.GetUsersAsync(Arg.Any<string?>(), 0, 8, Arg.Any<CancellationToken>())
            .Returns(
            [
                new FakeUser("u1", Username: "ada"),
                new FakeUser("u2", Email: "grace@x.io"),
                new FakeUser("u3"),
            ]);

        IReadOnlyList<MentionSuggestion> results =
            await Build().SearchAsync("x", 8, TestContext.Current.CancellationToken);

        results.Select(r => r.Label).ShouldBe(["ada", "grace@x.io", "u3"]);
    }

    [Fact]
    public async Task Search_passes_a_blank_query_as_null_to_the_reader()
    {
        _users.GetUsersAsync(null, 0, 5, Arg.Any<CancellationToken>()).Returns([]);

        await Build().SearchAsync("   ", 5, TestContext.Current.CancellationToken);

        await _users.Received(1).GetUsersAsync(null, 0, 5, Arg.Any<CancellationToken>());
    }
}
