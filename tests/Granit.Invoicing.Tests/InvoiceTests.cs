using Granit.Contacts.Domain.ValueObjects;
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
            ContactId.Create(Guid.NewGuid()),
            InvoiceDocumentType.Invoice,
            "EUR",
            CollectionMethod.Auto,
            BillingReason.SubscriptionCycle);

        invoice.AddLineItem(InvoiceLineItem.Create(
            Guid.NewGuid(), "Pro Plan - Monthly", 1, 29.99m,
            new LineItemSource(InvoiceSourceType.Subscription, Guid.NewGuid().ToString())));

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
    public void IssuedBillingAddressSnapshot_NullBeforeFinalize()
    {
        Invoice invoice = CreateDraftInvoice();

        invoice.IssuedBillingAddressSnapshot.ShouldBeNull();
    }

    [Fact]
    public void Finalize_WithBillingSnapshot_PersistsItOnAggregate()
    {
        Invoice invoice = CreateDraftInvoice();
        var snapshot = Granit.Contacts.Domain.BillingAddress.Create(
            line1: "rue 1",
            city: "Brussels",
            postalCode: "1000",
            country: "BE",
            companyName: "Acme",
            vatNumber: "BE0123456789");

        invoice.Finalize("INV-2026-0001", DateTimeOffset.UtcNow, dueAt: null, billingAddressSnapshot: snapshot);

        invoice.IssuedBillingAddressSnapshot.ShouldNotBeNull();
        invoice.IssuedBillingAddressSnapshot.Line1.ShouldBe("rue 1");
        invoice.IssuedBillingAddressSnapshot.VatNumber.ShouldBe("BE0123456789");
    }

    [Fact]
    public void Finalize_WithoutBillingSnapshot_LeavesSnapshotNull()
    {
        Invoice invoice = CreateDraftInvoice();

        invoice.Finalize("INV-2026-0001", DateTimeOffset.UtcNow, dueAt: null);

        invoice.IssuedBillingAddressSnapshot.ShouldBeNull();
    }

    [Fact]
    public void AddLineItem_AfterFinalize_ShouldThrow()
    {
        Invoice invoice = CreateDraftInvoice();
        invoice.Finalize("INV-001", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30));

        Should.Throw<InvalidOperationException>(() =>
            invoice.AddLineItem(InvoiceLineItem.Create(
                Guid.NewGuid(), "Extra", 1, 10m,
                new LineItemSource(InvoiceSourceType.OneShot))));
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
            ContactId.Create(Guid.NewGuid()),
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
    public void InvoiceId_Create_WithEmptyGuid_ShouldThrow() =>
        Should.Throw<ArgumentException>(() => InvoiceId.Create(Guid.Empty));

    // ======== SetTaxTotal ========

    [Fact]
    public void SetTaxTotal_ShouldRecalculateTotal()
    {
        Invoice invoice = CreateDraftInvoice();

        invoice.SetTaxTotal(6.30m);

        invoice.TaxTotal.ShouldBe(6.30m);
        invoice.Total.ShouldBe(29.99m + 6.30m);
    }

    [Fact]
    public void SetTaxTotal_ShouldUpdateAmountRemaining()
    {
        Invoice invoice = CreateDraftInvoice();

        invoice.SetTaxTotal(6.30m);

        invoice.AmountRemaining.ShouldBe(29.99m + 6.30m);
    }

    [Fact]
    public void SetTaxTotal_AfterFinalize_ShouldThrow()
    {
        Invoice invoice = CreateDraftInvoice();
        invoice.Finalize("INV-001", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30));

        Should.Throw<InvalidOperationException>(() => invoice.SetTaxTotal(5m));
    }

    // ======== VoidInvoice ========

    [Fact]
    public void VoidInvoice_WithPayments_ShouldThrow()
    {
        Invoice invoice = CreateDraftInvoice();
        invoice.Finalize("INV-001", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30));
        invoice.RecordPayment(10m, DateTimeOffset.UtcNow);

        Should.Throw<InvalidOperationException>(() => invoice.VoidInvoice());
    }

    [Fact]
    public void VoidInvoice_FromDraft_ShouldThrow()
    {
        Invoice invoice = CreateDraftInvoice();

        Should.Throw<InvalidOperationException>(() => invoice.VoidInvoice());
    }

    [Fact]
    public void VoidInvoice_WhenAlreadyVoid_ShouldReturnFalse()
    {
        Invoice invoice = CreateDraftInvoice();
        invoice.Finalize("INV-001", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30));
        invoice.VoidInvoice();

        bool result = invoice.VoidInvoice();

        result.ShouldBeFalse();
    }

    // ======== IsOverdue ========

    [Fact]
    public void IsOverdue_PastDue_ShouldReturnTrue()
    {
        Invoice invoice = CreateDraftInvoice();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        invoice.Finalize("INV-001", now.AddDays(-60), now.AddDays(-30));

        bool result = invoice.IsOverdue(now);

        result.ShouldBeTrue();
    }

    [Fact]
    public void IsOverdue_NotPastDue_ShouldReturnFalse()
    {
        Invoice invoice = CreateDraftInvoice();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        invoice.Finalize("INV-001", now, now.AddDays(30));

        bool result = invoice.IsOverdue(now);

        result.ShouldBeFalse();
    }

    [Fact]
    public void IsOverdue_WhenPaid_ShouldReturnFalse()
    {
        Invoice invoice = CreateDraftInvoice();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        invoice.Finalize("INV-001", now.AddDays(-60), now.AddDays(-30));
        invoice.RecordPayment(29.99m, now);

        bool result = invoice.IsOverdue(now);

        result.ShouldBeFalse();
    }

    // ======== Multiple line items ========

    [Fact]
    public void AddLineItem_Multiple_ShouldAccumulateSubtotal()
    {
        Invoice invoice = CreateDraftInvoice();

        invoice.AddLineItem(InvoiceLineItem.Create(
            Guid.NewGuid(), "Add-on", 1, 10m,
            new LineItemSource(InvoiceSourceType.OneShot)));

        invoice.Subtotal.ShouldBe(39.99m);
        invoice.Total.ShouldBe(39.99m);
    }

    // ======== AddExternalReference ========

    [Fact]
    public void AddExternalReference_ShouldAddToCollection()
    {
        Invoice invoice = CreateDraftInvoice();
        var reference = InvoiceExternalReference.Create(Guid.NewGuid(), "stripe", "in_123");

        invoice.AddExternalReference(reference);

        invoice.ExternalReferences.Count.ShouldBe(1);
        invoice.ExternalReferences[0].ProviderName.ShouldBe("stripe");
    }

    [Fact]
    public void AddExternalReference_WithNull_ShouldThrow()
    {
        Invoice invoice = CreateDraftInvoice();

        Should.Throw<ArgumentNullException>(() => invoice.AddExternalReference(null!));
    }

    // ======== AddDocument ========

    [Fact]
    public void AddDocument_ShouldAddToCollection()
    {
        Invoice invoice = CreateDraftInvoice();
        var document = InvoiceDocument.Create(
            Guid.NewGuid(), Guid.NewGuid(), "invoice.pdf", "application/pdf", DateTimeOffset.UtcNow);

        invoice.AddDocument(document);

        invoice.Documents.Count.ShouldBe(1);
        invoice.Documents[0].FileName.ShouldBe("invoice.pdf");
    }

    [Fact]
    public void AddDocument_WithNull_ShouldThrow()
    {
        Invoice invoice = CreateDraftInvoice();

        Should.Throw<ArgumentNullException>(() => invoice.AddDocument(null!));
    }

    // ======== MarkUncollectible ========

    [Fact]
    public void MarkUncollectible_FromOpen_ShouldReturnTrue()
    {
        Invoice invoice = CreateDraftInvoice();
        invoice.Finalize("INV-001", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30));

        bool result = invoice.MarkUncollectible();

        result.ShouldBeTrue();
        invoice.Status.ShouldBe(InvoiceStatus.Uncollectible);
    }

    [Fact]
    public void MarkUncollectible_WhenAlreadyUncollectible_ShouldReturnFalse()
    {
        Invoice invoice = CreateDraftInvoice();
        invoice.Finalize("INV-001", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30));
        invoice.MarkUncollectible();

        bool result = invoice.MarkUncollectible();

        result.ShouldBeFalse();
    }
}
