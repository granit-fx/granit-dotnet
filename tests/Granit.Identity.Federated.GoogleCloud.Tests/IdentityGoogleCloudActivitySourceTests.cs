using Granit.Identity.Federated.GoogleCloud.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.GoogleCloud.Tests;

public sealed class IdentityGoogleCloudActivitySourceTests
{
    [Fact]
    public void Name_IsCorrect() =>
        IdentityGoogleCloudActivitySource.Name.ShouldBe("Granit.Identity.GoogleCloud");

    [Fact]
    public void Source_HasCorrectName() =>
        IdentityGoogleCloudActivitySource.Source.Name.ShouldBe("Granit.Identity.GoogleCloud");

    [Fact]
    public void Operations_ListUsers_IsCorrect() =>
        IdentityGoogleCloudActivitySource.Operations.ListUsers.ShouldBe("firebase.list-users");

    [Fact]
    public void Operations_GetUser_IsCorrect() =>
        IdentityGoogleCloudActivitySource.Operations.GetUser.ShouldBe("firebase.get-user");

    [Fact]
    public void Operations_CreateUser_IsCorrect() =>
        IdentityGoogleCloudActivitySource.Operations.CreateUser.ShouldBe("firebase.create-user");

    [Fact]
    public void Operations_SetCustomClaims_IsCorrect() =>
        IdentityGoogleCloudActivitySource.Operations.SetCustomClaims.ShouldBe("firebase.set-custom-claims");

    [Fact]
    public void Operations_RevokeTokens_IsCorrect() =>
        IdentityGoogleCloudActivitySource.Operations.RevokeTokens.ShouldBe("firebase.revoke-tokens");

    [Fact]
    public void Tags_UserId_IsCorrect() =>
        IdentityGoogleCloudActivitySource.Tags.UserId.ShouldBe("firebase.user_id");

    [Fact]
    public void Tags_ProjectId_IsCorrect() =>
        IdentityGoogleCloudActivitySource.Tags.ProjectId.ShouldBe("firebase.project_id");
}
