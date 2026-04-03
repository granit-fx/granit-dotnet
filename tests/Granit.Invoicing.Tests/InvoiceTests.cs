using Granit.Invoicing.Domain;
using Granit.Invoicing.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.Invoicing.Tests;

public sealed class InvoiceTests
{
    private static Invoice CreateDraftInvoice()
    {
        var invoice = Invoice.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            InvoiceDocumentType.Invoice,
            "EUR",
            CollectionMethod.Auto,
            BillingReason.SubscriptionCycle);

        invoice.AddLineItem(InvoiceLineItem.Create(
            Guid.NewGuid(), "Pro Plan - Monthly", 1, 29.99m, InvoiceSourceType.Subscription));

        return invoice;
    }

    [Fact]
    public void Create_ShouldSetDraftStatus()
    {
        Invoice invoice = CreateDraftInvoice();

        invoice.Status.ShouldBe(InvoiceStatus.Draft);
        invoice.DocumentType.ShouldBe(InvoiceDocumentType.Invoice);
        invoice.Currency.ShouldBe("EUR");
    }

    [Fact]
    public void AddLineItem_ShouldRecalculateSubtotal()
    {
        Invoice invoice = CreateDraftInvoice();

        invoice.Subtotal.ShouldBe(29.99m);
        invoice.Total.ShouldBe(29.99m);
    }

    [Fact]
    public void Finalize_ShouldTransitionToFinalized()
    {
        Invoice invoice = CreateDraftInvoice();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        bool result = invoice.Finalize("INV-2026-0001", now, now.AddDays(30));

        result.ShouldBeTrue();
        invoice.Status.ShouldBe(InvoiceStatus.Open);
        invoice.InvoiceNumber.ShouldBe("INV-2026-0001");
        invoice.IssuedAt.ShouldBe(now);
    }

    [Fact]
    public void AddLineItem_AfterFinalize_ShouldThrow()
    {
        Invoice invoice = CreateDraftInvoice();
        invoice.Finalize("INV-001", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30));

        Should.Throw<InvalidOperationException>(() =>
            invoice.AddLineItem(InvoiceLineItem.Create(
                Guid.NewGuid(), "Extra", 1, 10m, InvoiceSourceType.OneShot)));
    }

    [Fact]
    public void RecordPayment_FullAmount_ShouldMarkPaid()
    {
        Invoice invoice = CreateDraftInvoice();
        invoice.Finalize("INV-001", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30));

        bool result = invoice.RecordPayment(29.99m, DateTimeOffset.UtcNow);

        result.ShouldBeTrue();
        invoice.Status.ShouldBe(InvoiceStatus.Paid);
        invoice.AmountPaid.ShouldBe(29.99m);
        invoice.AmountRemaining.ShouldBe(0m);
    }

    [Fact]
    public void RecordPayment_PartialAmount_ShouldKeepOpen()
    {
        Invoice invoice = CreateDraftInvoice();
        invoice.Finalize("INV-001", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30));

        bool result = invoice.RecordPayment(10m, DateTimeOffset.UtcNow);

        result.ShouldBeFalse(); // Returns false when not fully paid
        invoice.Status.ShouldBe(InvoiceStatus.Open);
        invoice.AmountPaid.ShouldBe(10m);
        invoice.AmountRemaining.ShouldBe(19.99m);
    }

    [Fact]
    public void RecordPayment_MultiplePartials_ShouldAccumulate()
    {
        Invoice invoice = CreateDraftInvoice();
        invoice.Finalize("INV-001", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30));

        invoice.RecordPayment(10m, DateTimeOffset.UtcNow);
        invoice.RecordPayment(19.99m, DateTimeOffset.UtcNow);

        invoice.Status.ShouldBe(InvoiceStatus.Paid);
        invoice.AmountPaid.ShouldBe(29.99m);
    }

    [Fact]
    public void RecordPayment_WithTolerance_ShouldMarkPaid()
    {
        Invoice invoice = CreateDraftInvoice();
        invoice.Finalize("INV-001", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30));

        bool result = invoice.RecordPayment(29.98m, DateTimeOffset.UtcNow, tolerance: 0.05m);

        result.ShouldBeTrue();
        invoice.Status.ShouldBe(InvoiceStatus.Paid);
    }

    [Fact]
    public void ApplyCreditNote_FullAmount_ShouldMarkPaid()
    {
        Invoice invoice = CreateDraftInvoice();
        invoice.Finalize("INV-001", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30));

        bool result = invoice.ApplyCreditNote(29.99m, DateTimeOffset.UtcNow);

        result.ShouldBeTrue();
        invoice.Status.ShouldBe(InvoiceStatus.Paid);
        invoice.AmountCredited.ShouldBe(29.99m);
        invoice.AmountPaid.ShouldBe(0m);
    }

    [Fact]
    public void RecordPayment_Overpayment_ShouldTrackOverpayment()
    {
        Invoice invoice = CreateDraftInvoice();
        invoice.Finalize("INV-001", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30));

        invoice.RecordPayment(35m, DateTimeOffset.UtcNow);

        invoice.Status.ShouldBe(InvoiceStatus.Paid);
        invoice.Overpayment.ShouldBe(5.01m);
    }

    [Fact]
    public void CreateCreditNote_ShouldSetDocumentType()
    {
        var creditNote = Invoice.CreateCreditNote(
            Guid.NewGuid(),
            Guid.NewGuid(),
            InvoiceId.Create(Guid.NewGuid()),
            "EUR",
            "Refund for defective service");

        creditNote.DocumentType.ShouldBe(InvoiceDocumentType.CreditNote);
        creditNote.IsCreditNote.ShouldBeTrue();
        creditNote.ParentInvoiceId.ShouldNotBeNull();
        creditNote.CreditNoteReason.ShouldBe("Refund for defective service");
    }

    [Fact]
    public void VoidInvoice_ShouldTransitionToVoided()
    {
        Invoice invoice = CreateDraftInvoice();
        invoice.Finalize("INV-001", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30));

        bool result = invoice.VoidInvoice();

        result.ShouldBeTrue();
        invoice.Status.ShouldBe(InvoiceStatus.Void);
    }

    [Fact]
    public void InvoiceId_Create_WithEmptyGuid_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => InvoiceId.Create(Guid.Empty));
    }
}
