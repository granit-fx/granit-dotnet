using System.Reflection;
using Shouldly;
using Xunit;

namespace Granit.Invoicing.Notifications.Tests.Templates;

/// <summary>
/// Pin the set of embedded templates shipped by the package. A renamed file or a missing
/// `.csproj` glob would break notification rendering at runtime — better to fail here.
/// </summary>
public sealed class EmbeddedTemplatesTests
{
    public static TheoryData<string> ExpectedTemplates() =>
    [
        // Neutral (= EN) variant, one per notification type.
        "Templates.Invoicing.CreditNoteIssued.html",
        "Templates.Invoicing.InvoiceIssued.html",
        "Templates.Invoicing.InvoiceOverdue.html",
        "Templates.Invoicing.InvoicePaid.html",
        // Czech (cs) — auto-translated, review before production.
        "Templates.Invoicing.CreditNoteIssued.cs.html",
        "Templates.Invoicing.InvoiceIssued.cs.html",
        "Templates.Invoicing.InvoiceOverdue.cs.html",
        "Templates.Invoicing.InvoicePaid.cs.html",
        // German (de) — auto-translated, review before production.
        "Templates.Invoicing.CreditNoteIssued.de.html",
        "Templates.Invoicing.InvoiceIssued.de.html",
        "Templates.Invoicing.InvoiceOverdue.de.html",
        "Templates.Invoicing.InvoicePaid.de.html",
        // English — Great Britain (en-GB) — auto-translated, review before production.
        "Templates.Invoicing.CreditNoteIssued.en-GB.html",
        "Templates.Invoicing.InvoiceIssued.en-GB.html",
        "Templates.Invoicing.InvoiceOverdue.en-GB.html",
        "Templates.Invoicing.InvoicePaid.en-GB.html",
        // Spanish (es) — auto-translated, review before production.
        "Templates.Invoicing.CreditNoteIssued.es.html",
        "Templates.Invoicing.InvoiceIssued.es.html",
        "Templates.Invoicing.InvoiceOverdue.es.html",
        "Templates.Invoicing.InvoicePaid.es.html",
        // French (fr) — second baseline culture shipped out of the box.
        "Templates.Invoicing.CreditNoteIssued.fr.html",
        "Templates.Invoicing.InvoiceIssued.fr.html",
        "Templates.Invoicing.InvoiceOverdue.fr.html",
        "Templates.Invoicing.InvoicePaid.fr.html",
        // French — Canada (fr-CA) — auto-translated, review before production.
        "Templates.Invoicing.CreditNoteIssued.fr-CA.html",
        "Templates.Invoicing.InvoiceIssued.fr-CA.html",
        "Templates.Invoicing.InvoiceOverdue.fr-CA.html",
        "Templates.Invoicing.InvoicePaid.fr-CA.html",
        // Hindi (hi) — auto-translated, review before production.
        "Templates.Invoicing.CreditNoteIssued.hi.html",
        "Templates.Invoicing.InvoiceIssued.hi.html",
        "Templates.Invoicing.InvoiceOverdue.hi.html",
        "Templates.Invoicing.InvoicePaid.hi.html",
        // Italian (it) — auto-translated, review before production.
        "Templates.Invoicing.CreditNoteIssued.it.html",
        "Templates.Invoicing.InvoiceIssued.it.html",
        "Templates.Invoicing.InvoiceOverdue.it.html",
        "Templates.Invoicing.InvoicePaid.it.html",
        // Japanese (ja) — auto-translated, review before production.
        "Templates.Invoicing.CreditNoteIssued.ja.html",
        "Templates.Invoicing.InvoiceIssued.ja.html",
        "Templates.Invoicing.InvoiceOverdue.ja.html",
        "Templates.Invoicing.InvoicePaid.ja.html",
        // Korean (ko) — auto-translated, review before production.
        "Templates.Invoicing.CreditNoteIssued.ko.html",
        "Templates.Invoicing.InvoiceIssued.ko.html",
        "Templates.Invoicing.InvoiceOverdue.ko.html",
        "Templates.Invoicing.InvoicePaid.ko.html",
        // Dutch (nl) — auto-translated, review before production.
        "Templates.Invoicing.CreditNoteIssued.nl.html",
        "Templates.Invoicing.InvoiceIssued.nl.html",
        "Templates.Invoicing.InvoiceOverdue.nl.html",
        "Templates.Invoicing.InvoicePaid.nl.html",
        // Polish (pl) — auto-translated, review before production.
        "Templates.Invoicing.CreditNoteIssued.pl.html",
        "Templates.Invoicing.InvoiceIssued.pl.html",
        "Templates.Invoicing.InvoiceOverdue.pl.html",
        "Templates.Invoicing.InvoicePaid.pl.html",
        // Portuguese (pt) — auto-translated, review before production.
        "Templates.Invoicing.CreditNoteIssued.pt.html",
        "Templates.Invoicing.InvoiceIssued.pt.html",
        "Templates.Invoicing.InvoiceOverdue.pt.html",
        "Templates.Invoicing.InvoicePaid.pt.html",
        // Portuguese — Brazil (pt-BR) — auto-translated, review before production.
        "Templates.Invoicing.CreditNoteIssued.pt-BR.html",
        "Templates.Invoicing.InvoiceIssued.pt-BR.html",
        "Templates.Invoicing.InvoiceOverdue.pt-BR.html",
        "Templates.Invoicing.InvoicePaid.pt-BR.html",
        // Swedish (sv) — auto-translated, review before production.
        "Templates.Invoicing.CreditNoteIssued.sv.html",
        "Templates.Invoicing.InvoiceIssued.sv.html",
        "Templates.Invoicing.InvoiceOverdue.sv.html",
        "Templates.Invoicing.InvoicePaid.sv.html",
        // Turkish (tr) — auto-translated, review before production.
        "Templates.Invoicing.CreditNoteIssued.tr.html",
        "Templates.Invoicing.InvoiceIssued.tr.html",
        "Templates.Invoicing.InvoiceOverdue.tr.html",
        "Templates.Invoicing.InvoicePaid.tr.html",
        // Chinese Simplified (zh) — auto-translated, review before production.
        "Templates.Invoicing.CreditNoteIssued.zh.html",
        "Templates.Invoicing.InvoiceIssued.zh.html",
        "Templates.Invoicing.InvoiceOverdue.zh.html",
        "Templates.Invoicing.InvoicePaid.zh.html",
    ];

    [Theory]
    [MemberData(nameof(ExpectedTemplates))]
    public void EachExpectedTemplate_IsEmbeddedInTheAssembly(string suffix)
    {
        Assembly assembly = typeof(GranitInvoicingNotificationsModule).Assembly;
        string assemblyName = assembly.GetName().Name!;
        string fullResourceName = $"{assemblyName}.{suffix}";

        string[] resources = assembly.GetManifestResourceNames();

        resources.ShouldContain(
            fullResourceName,
            customMessage: $"Embedded resource '{fullResourceName}' is missing. Check the <EmbeddedResource> glob in the .csproj and the file presence under Templates/.");
    }

    [Theory]
    [MemberData(nameof(ExpectedTemplates))]
    public void EachExpectedTemplate_IsNotEmpty(string suffix)
    {
        Assembly assembly = typeof(GranitInvoicingNotificationsModule).Assembly;
        string fullResourceName = $"{assembly.GetName().Name}.{suffix}";

        using Stream? stream = assembly.GetManifestResourceStream(fullResourceName);
        stream.ShouldNotBeNull($"Resource '{fullResourceName}' should be loadable.");
        stream.Length.ShouldBeGreaterThan(0, $"Resource '{fullResourceName}' should not be empty.");
    }
}
