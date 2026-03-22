using Granit.Identity.Federated.Cognito.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Cognito.Tests;

public sealed class IdentityCognitoActivitySourceTests
{
    [Fact]
    public void Source_HasCorrectName() =>
        IdentityCognitoActivitySource.Source.Name.ShouldBe("Granit.Identity.Cognito");

    [Fact]
    public void Operations_ListUsers_HasCorrectValue() =>
        IdentityCognitoActivitySource.Operations.ListUsers.ShouldBe("cognito.list-users");

    [Fact]
    public void Operations_GetUser_HasCorrectValue() =>
        IdentityCognitoActivitySource.Operations.GetUser.ShouldBe("cognito.get-user");

    [Fact]
    public void Operations_VerifyCredentials_HasCorrectValue() =>
        IdentityCognitoActivitySource.Operations.VerifyCredentials.ShouldBe("cognito.verify-credentials");
}
