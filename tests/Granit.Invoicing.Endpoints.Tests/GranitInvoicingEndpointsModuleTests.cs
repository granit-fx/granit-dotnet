using Granit.Invoicing.Endpoints.Permissions;
using Shouldly;
using Xunit;

namespace Granit.Invoicing.Endpoints.Tests;

public sealed class InvoicingPermissionsTests
{
    [Fact]
    public void GroupName_IsInvoicing() =>
        InvoicingPermissions.GroupName.ShouldBe("Invoicing");

    [Fact]
    public void InvoicesRead_FollowsThreeSegmentFormat()
    {
        InvoicingPermissions.Invoices.Read.ShouldBe("Invoicing.Invoices.Read");
        InvoicingPermissions.Invoices.Read.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void InvoicesManage_FollowsThreeSegmentFormat()
    {
        InvoicingPermissions.Invoices.Manage.ShouldBe("Invoicing.Invoices.Manage");
        InvoicingPermissions.Invoices.Manage.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void InvoicesDownload_FollowsThreeSegmentFormat()
    {
        InvoicingPermissions.Invoices.Download.ShouldBe("Invoicing.Invoices.Download");
        InvoicingPermissions.Invoices.Download.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void CreditNotesRead_FollowsThreeSegmentFormat()
    {
        InvoicingPermissions.CreditNotes.Read.ShouldBe("Invoicing.CreditNotes.Read");
        InvoicingPermissions.CreditNotes.Read.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void CreditNotesManage_FollowsThreeSegmentFormat()
    {
        InvoicingPermissions.CreditNotes.Manage.ShouldBe("Invoicing.CreditNotes.Manage");
        InvoicingPermissions.CreditNotes.Manage.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void AllPermissions_StartWithGroupName()
    {
        string prefix = InvoicingPermissions.GroupName + ".";

        InvoicingPermissions.Invoices.Read.ShouldStartWith(prefix);
        InvoicingPermissions.Invoices.Manage.ShouldStartWith(prefix);
        InvoicingPermissions.Invoices.Download.ShouldStartWith(prefix);
        InvoicingPermissions.CreditNotes.Read.ShouldStartWith(prefix);
        InvoicingPermissions.CreditNotes.Manage.ShouldStartWith(prefix);
    }
}
