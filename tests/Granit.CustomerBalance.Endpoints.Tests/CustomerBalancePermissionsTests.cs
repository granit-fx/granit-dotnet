using Granit.CustomerBalance.Endpoints.Permissions;
using Shouldly;
using Xunit;

namespace Granit.CustomerBalance.Endpoints.Tests;

public sealed class CustomerBalancePermissionsTests
{
    [Fact]
    public void GroupName_ShouldBeCustomerBalance()
    {
        CustomerBalancePermissions.GroupName.ShouldBe("CustomerBalance");
    }

    [Theory]
    [InlineData(nameof(CustomerBalancePermissions.Accounts), CustomerBalancePermissions.Accounts.Read)]
    [InlineData(nameof(CustomerBalancePermissions.Transactions), CustomerBalancePermissions.Transactions.Read)]
    [InlineData(nameof(CustomerBalancePermissions.Credits), CustomerBalancePermissions.Credits.Manage)]
    public void Permission_ShouldFollowThreeSegmentFormat(string resource, string permission)
    {
        _ = resource;
        string[] segments = permission.Split('.');
        segments.Length.ShouldBe(3, $"Permission '{permission}' must have exactly 3 segments (Group.Resource.Action).");
    }

    [Theory]
    [InlineData(CustomerBalancePermissions.Accounts.Read)]
    [InlineData(CustomerBalancePermissions.Transactions.Read)]
    [InlineData(CustomerBalancePermissions.Credits.Manage)]
    public void Permission_ShouldStartWithGroupName(string permission)
    {
        permission.ShouldStartWith(CustomerBalancePermissions.GroupName + ".");
    }
}
