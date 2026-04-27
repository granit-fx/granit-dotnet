using System.Reflection;
using Shouldly;
using Xunit;

namespace Granit.Payments.Notifications.Tests.Templates;

/// <summary>
/// Pin the set of embedded templates shipped by the package. A renamed file or a missing
/// `.csproj` glob would break notification rendering at runtime — better to fail here.
/// </summary>
public sealed class EmbeddedTemplatesTests
{
    public static TheoryData<string> ExpectedTemplates() =>
    [
        // Neutral (= EN) variant, one per notification type.
        "Templates.Payments.DisputeOpened.html",
        "Templates.Payments.PaymentFailed.html",
        "Templates.Payments.PaymentMethodExpiring.html",
        "Templates.Payments.PaymentSucceeded.html",
        "Templates.Payments.RefundProcessed.html",
        // Czech (cs) — auto-translated, review before production.
        "Templates.Payments.DisputeOpened.cs.html",
        "Templates.Payments.PaymentFailed.cs.html",
        "Templates.Payments.PaymentMethodExpiring.cs.html",
        "Templates.Payments.PaymentSucceeded.cs.html",
        "Templates.Payments.RefundProcessed.cs.html",
        // German (de) — auto-translated, review before production.
        "Templates.Payments.DisputeOpened.de.html",
        "Templates.Payments.PaymentFailed.de.html",
        "Templates.Payments.PaymentMethodExpiring.de.html",
        "Templates.Payments.PaymentSucceeded.de.html",
        "Templates.Payments.RefundProcessed.de.html",
        // English (en-GB) — auto-translated, review before production.
        "Templates.Payments.DisputeOpened.en-GB.html",
        "Templates.Payments.PaymentFailed.en-GB.html",
        "Templates.Payments.PaymentMethodExpiring.en-GB.html",
        "Templates.Payments.PaymentSucceeded.en-GB.html",
        "Templates.Payments.RefundProcessed.en-GB.html",
        // Spanish (es) — auto-translated, review before production.
        "Templates.Payments.DisputeOpened.es.html",
        "Templates.Payments.PaymentFailed.es.html",
        "Templates.Payments.PaymentMethodExpiring.es.html",
        "Templates.Payments.PaymentSucceeded.es.html",
        "Templates.Payments.RefundProcessed.es.html",
        // French (fr) — second baseline culture shipped out of the box.
        "Templates.Payments.DisputeOpened.fr.html",
        "Templates.Payments.PaymentFailed.fr.html",
        "Templates.Payments.PaymentMethodExpiring.fr.html",
        "Templates.Payments.PaymentSucceeded.fr.html",
        "Templates.Payments.RefundProcessed.fr.html",
        // French Canadian (fr-CA) — auto-translated, review before production.
        "Templates.Payments.DisputeOpened.fr-CA.html",
        "Templates.Payments.PaymentFailed.fr-CA.html",
        "Templates.Payments.PaymentMethodExpiring.fr-CA.html",
        "Templates.Payments.PaymentSucceeded.fr-CA.html",
        "Templates.Payments.RefundProcessed.fr-CA.html",
        // Hindi (hi) — auto-translated, review before production.
        "Templates.Payments.DisputeOpened.hi.html",
        "Templates.Payments.PaymentFailed.hi.html",
        "Templates.Payments.PaymentMethodExpiring.hi.html",
        "Templates.Payments.PaymentSucceeded.hi.html",
        "Templates.Payments.RefundProcessed.hi.html",
        // Italian (it) — auto-translated, review before production.
        "Templates.Payments.DisputeOpened.it.html",
        "Templates.Payments.PaymentFailed.it.html",
        "Templates.Payments.PaymentMethodExpiring.it.html",
        "Templates.Payments.PaymentSucceeded.it.html",
        "Templates.Payments.RefundProcessed.it.html",
        // Japanese (ja) — auto-translated, review before production.
        "Templates.Payments.DisputeOpened.ja.html",
        "Templates.Payments.PaymentFailed.ja.html",
        "Templates.Payments.PaymentMethodExpiring.ja.html",
        "Templates.Payments.PaymentSucceeded.ja.html",
        "Templates.Payments.RefundProcessed.ja.html",
        // Korean (ko) — auto-translated, review before production.
        "Templates.Payments.DisputeOpened.ko.html",
        "Templates.Payments.PaymentFailed.ko.html",
        "Templates.Payments.PaymentMethodExpiring.ko.html",
        "Templates.Payments.PaymentSucceeded.ko.html",
        "Templates.Payments.RefundProcessed.ko.html",
        // Dutch (nl) — auto-translated, review before production.
        "Templates.Payments.DisputeOpened.nl.html",
        "Templates.Payments.PaymentFailed.nl.html",
        "Templates.Payments.PaymentMethodExpiring.nl.html",
        "Templates.Payments.PaymentSucceeded.nl.html",
        "Templates.Payments.RefundProcessed.nl.html",
        // Polish (pl) — auto-translated, review before production.
        "Templates.Payments.DisputeOpened.pl.html",
        "Templates.Payments.PaymentFailed.pl.html",
        "Templates.Payments.PaymentMethodExpiring.pl.html",
        "Templates.Payments.PaymentSucceeded.pl.html",
        "Templates.Payments.RefundProcessed.pl.html",
        // Portuguese (pt) — auto-translated, review before production.
        "Templates.Payments.DisputeOpened.pt.html",
        "Templates.Payments.PaymentFailed.pt.html",
        "Templates.Payments.PaymentMethodExpiring.pt.html",
        "Templates.Payments.PaymentSucceeded.pt.html",
        "Templates.Payments.RefundProcessed.pt.html",
        // Portuguese Brazilian (pt-BR) — auto-translated, review before production.
        "Templates.Payments.DisputeOpened.pt-BR.html",
        "Templates.Payments.PaymentFailed.pt-BR.html",
        "Templates.Payments.PaymentMethodExpiring.pt-BR.html",
        "Templates.Payments.PaymentSucceeded.pt-BR.html",
        "Templates.Payments.RefundProcessed.pt-BR.html",
        // Swedish (sv) — auto-translated, review before production.
        "Templates.Payments.DisputeOpened.sv.html",
        "Templates.Payments.PaymentFailed.sv.html",
        "Templates.Payments.PaymentMethodExpiring.sv.html",
        "Templates.Payments.PaymentSucceeded.sv.html",
        "Templates.Payments.RefundProcessed.sv.html",
        // Turkish (tr) — auto-translated, review before production.
        "Templates.Payments.DisputeOpened.tr.html",
        "Templates.Payments.PaymentFailed.tr.html",
        "Templates.Payments.PaymentMethodExpiring.tr.html",
        "Templates.Payments.PaymentSucceeded.tr.html",
        "Templates.Payments.RefundProcessed.tr.html",
        // Chinese Simplified (zh) — auto-translated, review before production.
        "Templates.Payments.DisputeOpened.zh.html",
        "Templates.Payments.PaymentFailed.zh.html",
        "Templates.Payments.PaymentMethodExpiring.zh.html",
        "Templates.Payments.PaymentSucceeded.zh.html",
        "Templates.Payments.RefundProcessed.zh.html",
    ];

    [Theory]
    [MemberData(nameof(ExpectedTemplates))]
    public void EachExpectedTemplate_IsEmbeddedInTheAssembly(string suffix)
    {
        Assembly assembly = typeof(GranitPaymentsNotificationsModule).Assembly;
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
        Assembly assembly = typeof(GranitPaymentsNotificationsModule).Assembly;
        string fullResourceName = $"{assembly.GetName().Name}.{suffix}";

        using Stream? stream = assembly.GetManifestResourceStream(fullResourceName);
        stream.ShouldNotBeNull($"Resource '{fullResourceName}' should be loadable.");
        stream.Length.ShouldBeGreaterThan(0, $"Resource '{fullResourceName}' should not be empty.");
    }
}
