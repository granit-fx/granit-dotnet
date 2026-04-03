using Granit.Payments.Endpoints.Permissions;
using Shouldly;
using Xunit;

namespace Granit.Payments.Endpoints.Tests;

public sealed class PaymentsPermissionsTests
{
    [Fact]
    public void GroupName_IsPayments() =>
        PaymentsPermissions.GroupName.ShouldBe("Payments");

    [Fact]
    public void TransactionsRead_FollowsThreeSegmentFormat()
    {
        PaymentsPermissions.Transactions.Read.ShouldBe("Payments.Transactions.Read");
        PaymentsPermissions.Transactions.Read.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void ChargesExecute_FollowsThreeSegmentFormat()
    {
        PaymentsPermissions.Charges.Execute.ShouldBe("Payments.Charges.Execute");
        PaymentsPermissions.Charges.Execute.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void RefundsExecute_FollowsThreeSegmentFormat()
    {
        PaymentsPermissions.Refunds.Execute.ShouldBe("Payments.Refunds.Execute");
        PaymentsPermissions.Refunds.Execute.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void MethodsRead_FollowsThreeSegmentFormat()
    {
        PaymentsPermissions.Methods.Read.ShouldBe("Payments.Methods.Read");
        PaymentsPermissions.Methods.Read.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void MethodsManage_FollowsThreeSegmentFormat()
    {
        PaymentsPermissions.Methods.Manage.ShouldBe("Payments.Methods.Manage");
        PaymentsPermissions.Methods.Manage.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void AllPermissions_StartWithGroupName()
    {
        string prefix = PaymentsPermissions.GroupName + ".";

        PaymentsPermissions.Transactions.Read.ShouldStartWith(prefix);
        PaymentsPermissions.Charges.Execute.ShouldStartWith(prefix);
        PaymentsPermissions.Refunds.Execute.ShouldStartWith(prefix);
        PaymentsPermissions.Methods.Read.ShouldStartWith(prefix);
        PaymentsPermissions.Methods.Manage.ShouldStartWith(prefix);
    }
}
