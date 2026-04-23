using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Granit.Events;
using Granit.Identity;
using Granit.Identity.Federated.Cognito.Internal;
using Granit.Identity.Federated.Cognito.Options;
using Granit.Identity.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Cognito.Tests;

/// <summary>
/// Covers Phase 2 <see cref="IIdentityClientRoleManager"/> methods on
/// <see cref="CognitoIdentityProvider"/> — mock-based (same pattern as
/// <see cref="CognitoIdentityProviderTests"/>).
/// </summary>
public sealed class CognitoClientRoleTests
{
    private readonly IAmazonCognitoIdentityProvider _cognitoClient =
        Substitute.For<IAmazonCognitoIdentityProvider>();
    private readonly IDistributedEventBus _distributedEventBus =
        Substitute.For<IDistributedEventBus>();

    private CognitoIdentityProvider BuildSut(string delimiter = ":")
    {
        CognitoAdminOptions adminOpts = new()
        {
            Region = "eu-west-1",
            UserPoolId = "eu-west-1_TEST",
            AppClientId = "admin-client",
        };
        CognitoClientRoleSyncOptions syncOpts = new()
        {
            Delimiter = delimiter,
        };
        return new CognitoIdentityProvider(
            _cognitoClient,
            Microsoft.Extensions.Options.Options.Create(adminOpts),
            Microsoft.Extensions.Options.Options.Create(syncOpts),
            _distributedEventBus,
            NullLogger<CognitoIdentityProvider>.Instance);
    }

    [Fact]
    public async Task GetClientsAsync_ReturnsAppClientIds_FromListUserPoolClients()
    {
        _cognitoClient.ListUserPoolClientsAsync(
                Arg.Any<ListUserPoolClientsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ListUserPoolClientsResponse
            {
                UserPoolClients =
                [
                    new UserPoolClientDescription { ClientId = "clientA", ClientName = "App A" },
                    new UserPoolClientDescription { ClientId = "clientB", ClientName = "App B" },
                ],
                NextToken = null,
            });
        CognitoIdentityProvider sut = BuildSut();

        IReadOnlyList<string> clients = await sut.GetClientsAsync(
            TestContext.Current.CancellationToken);

        clients.ShouldBe(["clientA", "clientB"]);
    }

    [Fact]
    public async Task GetClientRolesAsync_FiltersByPrefix_StripsAndPopulatesClientId()
    {
        _cognitoClient.ListGroupsAsync(
                Arg.Any<ListGroupsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ListGroupsResponse
            {
                Groups =
                [
                    new GroupType { GroupName = "clientA:editor", Description = "Edit docs" },
                    new GroupType { GroupName = "clientA:viewer", Description = null },
                    new GroupType { GroupName = "clientB:admin", Description = null },
                    new GroupType { GroupName = "platform-admins", Description = "Realm role" },
                ],
                NextToken = null,
            });
        CognitoIdentityProvider sut = BuildSut();

        IReadOnlyList<IdentityRole> roles = await sut.GetClientRolesAsync(
            "clientA", TestContext.Current.CancellationToken);

        roles.Count.ShouldBe(2);
        roles[0].Name.ShouldBe("editor");
        roles[0].ClientId.ShouldBe("clientA");
        roles[0].Description.ShouldBe("Edit docs");
        roles[1].Name.ShouldBe("viewer");
        roles[1].ClientId.ShouldBe("clientA");
    }

    [Fact]
    public async Task GetClientRolesAsync_CustomDelimiter_StripsCorrectly()
    {
        _cognitoClient.ListGroupsAsync(
                Arg.Any<ListGroupsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ListGroupsResponse
            {
                Groups =
                [
                    new GroupType { GroupName = "clientA__admin", Description = null },
                    new GroupType { GroupName = "clientA:editor", Description = null },
                ],
                NextToken = null,
            });
        CognitoIdentityProvider sut = BuildSut(delimiter: "__");

        IReadOnlyList<IdentityRole> roles = await sut.GetClientRolesAsync(
            "clientA", TestContext.Current.CancellationToken);

        roles.Count.ShouldBe(1);
        roles[0].Name.ShouldBe("admin");
    }

    [Fact]
    public async Task GetClientRolesAsync_NoMatchingGroups_ReturnsEmpty()
    {
        _cognitoClient.ListGroupsAsync(
                Arg.Any<ListGroupsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ListGroupsResponse
            {
                Groups =
                [
                    new GroupType { GroupName = "realm-only-group" },
                ],
                NextToken = null,
            });
        CognitoIdentityProvider sut = BuildSut();

        IReadOnlyList<IdentityRole> roles = await sut.GetClientRolesAsync(
            "clientA", TestContext.Current.CancellationToken);

        roles.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetUserClientRolesAsync_FiltersByPrefix_PopulatesClientId()
    {
        _cognitoClient.AdminListGroupsForUserAsync(
                Arg.Any<AdminListGroupsForUserRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AdminListGroupsForUserResponse
            {
                Groups =
                [
                    new GroupType { GroupName = "clientA:admin", Description = null },
                    new GroupType { GroupName = "platform-admins", Description = null },
                ],
                NextToken = null,
            });
        CognitoIdentityProvider sut = BuildSut();

        IReadOnlyList<IdentityRole> roles = await sut.GetUserClientRolesAsync(
            "user-42", "clientA", TestContext.Current.CancellationToken);

        roles.Count.ShouldBe(1);
        roles[0].Name.ShouldBe("admin");
        roles[0].ClientId.ShouldBe("clientA");
    }

    // ──── ADR-031 — client-role writes ────────────────────────────────────

    [Fact]
    public async Task CreateClientRoleAsync_CreatesGroup_WithPrefixedName()
    {
        _cognitoClient.CreateGroupAsync(
                Arg.Any<CreateGroupRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                CreateGroupRequest req = ci.Arg<CreateGroupRequest>();
                return new CreateGroupResponse
                {
                    Group = new GroupType
                    {
                        GroupName = req.GroupName,
                        Description = req.Description,
                    },
                };
            });
        CognitoIdentityProvider sut = BuildSut();

        IdentityRole created = await sut.CreateClientRoleAsync(
            "clientA", "editor", "Edit docs", TestContext.Current.CancellationToken);

        created.Id.ShouldBe("clientA:editor");
        created.Name.ShouldBe("editor");
        created.ClientId.ShouldBe("clientA");
        created.Description.ShouldBe("Edit docs");

        await _cognitoClient.Received(1).CreateGroupAsync(
            Arg.Is<CreateGroupRequest>(r =>
                r.UserPoolId == "eu-west-1_TEST" &&
                r.GroupName == "clientA:editor" &&
                r.Description == "Edit docs"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateClientRoleAsync_CustomDelimiter_UsesDelimiterInGroupName()
    {
        _cognitoClient.CreateGroupAsync(
                Arg.Any<CreateGroupRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci => new CreateGroupResponse
            {
                Group = new GroupType
                {
                    GroupName = ci.Arg<CreateGroupRequest>().GroupName,
                    Description = ci.Arg<CreateGroupRequest>().Description,
                },
            });
        CognitoIdentityProvider sut = BuildSut(delimiter: "__");

        IdentityRole created = await sut.CreateClientRoleAsync(
            "clientA", "viewer", null, TestContext.Current.CancellationToken);

        created.Id.ShouldBe("clientA__viewer");
        created.Name.ShouldBe("viewer");
    }

    [Fact]
    public async Task AssignClientRoleAsync_AddsUserToPrefixedGroup()
    {
        _cognitoClient.AdminAddUserToGroupAsync(
                Arg.Any<AdminAddUserToGroupRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AdminAddUserToGroupResponse());
        CognitoIdentityProvider sut = BuildSut();

        await sut.AssignClientRoleAsync(
            "user-42", "clientA", "editor", TestContext.Current.CancellationToken);

        await _cognitoClient.Received(1).AdminAddUserToGroupAsync(
            Arg.Is<AdminAddUserToGroupRequest>(r =>
                r.UserPoolId == "eu-west-1_TEST" &&
                r.Username == "user-42" &&
                r.GroupName == "clientA:editor"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveClientRoleAsync_RemovesUserFromPrefixedGroup()
    {
        _cognitoClient.AdminRemoveUserFromGroupAsync(
                Arg.Any<AdminRemoveUserFromGroupRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AdminRemoveUserFromGroupResponse());
        CognitoIdentityProvider sut = BuildSut();

        await sut.RemoveClientRoleAsync(
            "user-42", "clientA", "editor", TestContext.Current.CancellationToken);

        await _cognitoClient.Received(1).AdminRemoveUserFromGroupAsync(
            Arg.Is<AdminRemoveUserFromGroupRequest>(r =>
                r.UserPoolId == "eu-west-1_TEST" &&
                r.Username == "user-42" &&
                r.GroupName == "clientA:editor"),
            Arg.Any<CancellationToken>());
    }
}
