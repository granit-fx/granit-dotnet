using Granit.Notifications;
using Shouldly;
using Xunit;

namespace Granit.Invoicing.Notifications.Tests;

public sealed class InvoicingNotificationTypeTests
{
    [Fact]
    public void InvoiceIssued_ShouldHaveCorrectMetadata()
    {
        InvoiceIssuedNotificationType type = InvoiceIssuedNotificationType.Instance;

        type.Name.ShouldBe("Invoicing.InvoiceIssued");
        type.DefaultSeverity.ShouldBe(NotificationSeverity.Info);
        type.DefaultChannels.ShouldContain(NotificationChannels.Email);
        type.DefaultChannels.ShouldContain(NotificationChannels.InApp);
        type.DefaultChannels.Count.ShouldBe(2);
    }

    [Fact]
    public void InvoiceIssued_ShouldBeSingleton() =>
        InvoiceIssuedNotificationType.Instance.ShouldBeSameAs(InvoiceIssuedNotificationType.Instance);

    [Fact]
    public void InvoiceIssuedData_ShouldCreateRecord()
    {
        var invoiceId = Guid.NewGuid();
        DateTimeOffset issuedAt = DateTimeOffset.UtcNow;
        DateTimeOffset dueAt = DateTimeOffset.UtcNow.AddDays(30);

        var data = new InvoiceIssuedNotificationData(
            invoiceId, "INV-2026-001", 250.00m, "EUR", issuedAt, dueAt);

        data.InvoiceId.ShouldBe(invoiceId);
        data.InvoiceNumber.ShouldBe("INV-2026-001");
        data.Total.ShouldBe(250.00m);
        data.Currency.ShouldBe("EUR");
        data.IssuedAt.ShouldBe(issuedAt);
        data.DueAt.ShouldBe(dueAt);
    }

    [Fact]
    public void InvoiceIssuedData_ShouldAllowNullDueAt()
    {
        var data = new InvoiceIssuedNotificationData(
            Guid.NewGuid(), "INV-2026-002", 100.00m, "EUR", DateTimeOffset.UtcNow, null);

        data.DueAt.ShouldBeNull();
    }

    [Fact]
    public void InvoiceOverdue_ShouldHaveCorrectMetadata()
    {
        InvoiceOverdueNotificationType type = InvoiceOverdueNotificationType.Instance;

        type.Name.ShouldBe("Invoicing.InvoiceOverdue");
        type.DefaultSeverity.ShouldBe(NotificationSeverity.Warning);
        type.DefaultChannels.ShouldContain(NotificationChannels.Email);
        type.DefaultChannels.ShouldContain(NotificationChannels.InApp);
        type.DefaultChannels.Count.ShouldBe(2);
    }

    [Fact]
    public void InvoiceOverdue_ShouldBeSingleton() =>
        InvoiceOverdueNotificationType.Instance.ShouldBeSameAs(InvoiceOverdueNotificationType.Instance);

    [Fact]
    public void InvoiceOverdueData_ShouldCreateRecord()
    {
        var invoiceId = Guid.NewGuid();
        DateTimeOffset dueAt = DateTimeOffset.UtcNow.AddDays(-15);

        var data = new InvoiceOverdueNotificationData(
            invoiceId, "INV-2026-003", 175.50m, "EUR", dueAt, 15);

        data.InvoiceId.ShouldBe(invoiceId);
        data.InvoiceNumber.ShouldBe("INV-2026-003");
        data.AmountRemaining.ShouldBe(175.50m);
        data.Currency.ShouldBe("EUR");
        data.DueAt.ShouldBe(dueAt);
        data.DaysOverdue.ShouldBe(15);
    }

    [Fact]
    public void InvoicePaid_ShouldHaveCorrectMetadata()
    {
        InvoicePaidNotificationType type = InvoicePaidNotificationType.Instance;

        type.Name.ShouldBe("Invoicing.InvoicePaid");
        type.DefaultSeverity.ShouldBe(NotificationSeverity.Success);
        type.DefaultChannels.ShouldContain(NotificationChannels.Email);
        type.DefaultChannels.ShouldContain(NotificationChannels.InApp);
        type.DefaultChannels.Count.ShouldBe(2);
    }

    [Fact]
    public void InvoicePaid_ShouldBeSingleton() =>
        InvoicePaidNotificationType.Instance.ShouldBeSameAs(InvoicePaidNotificationType.Instance);

    [Fact]
    public void InvoicePaidData_ShouldCreateRecord()
    {
        var invoiceId = Guid.NewGuid();
        DateTimeOffset paidAt = DateTimeOffset.UtcNow;

        var data = new InvoicePaidNotificationData(
            invoiceId, "INV-2026-004", 500.00m, "USD", paidAt);

        data.InvoiceId.ShouldBe(invoiceId);
        data.InvoiceNumber.ShouldBe("INV-2026-004");
        data.Total.ShouldBe(500.00m);
        data.Currency.ShouldBe("USD");
        data.PaidAt.ShouldBe(paidAt);
    }

    [Fact]
    public void CreditNoteIssued_ShouldHaveCorrectMetadata()
    {
        CreditNoteIssuedNotificationType type = CreditNoteIssuedNotificationType.Instance;

        type.Name.ShouldBe("Invoicing.CreditNoteIssued");
        type.DefaultSeverity.ShouldBe(NotificationSeverity.Info);
        type.DefaultChannels.ShouldContain(NotificationChannels.Email);
        type.DefaultChannels.ShouldContain(NotificationChannels.InApp);
        type.DefaultChannels.Count.ShouldBe(2);
    }

    [Fact]
    public void CreditNoteIssued_ShouldBeSingleton()
    {
        CreditNoteIssuedNotificationType.Instance
            .ShouldBeSameAs(CreditNoteIssuedNotificationType.Instance);
    }

    [Fact]
    public void CreditNoteIssuedData_ShouldCreateRecord()
    {
        var creditNoteId = Guid.NewGuid();
        var parentInvoiceId = Guid.NewGuid();

        var data = new CreditNoteIssuedNotificationData(
            creditNoteId, "CN-2026-001", parentInvoiceId, "INV-2026-005",
            75.00m, "EUR", "Duplicate charge");

        data.CreditNoteId.ShouldBe(creditNoteId);
        data.CreditNoteNumber.ShouldBe("CN-2026-001");
        data.ParentInvoiceId.ShouldBe(parentInvoiceId);
        data.ParentInvoiceNumber.ShouldBe("INV-2026-005");
        data.Amount.ShouldBe(75.00m);
        data.Currency.ShouldBe("EUR");
        data.Reason.ShouldBe("Duplicate charge");
    }
}
