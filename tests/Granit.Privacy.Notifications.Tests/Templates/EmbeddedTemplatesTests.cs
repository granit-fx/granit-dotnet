using System.Reflection;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Notifications.Tests.Templates;

/// <summary>
/// Pin the set of embedded templates shipped by the package. A renamed file or a missing
/// `.csproj` glob would break notification rendering at runtime — better to fail here.
/// </summary>
public sealed class EmbeddedTemplatesTests
{
    public static TheoryData<string> ExpectedTemplates() =>
    [
        // Neutral (= EN) variant, one per notification type.
        "Templates.Privacy.LegalDocumentObsolete.html",
        "Templates.privacy.deletion_acknowledged.html",
        "Templates.privacy.deletion_cancelled.html",
        "Templates.privacy.deletion_confirmed.html",
        "Templates.privacy.deletion_deferred_confirmed.html",
        "Templates.privacy.deletion_reminder.html",
        "Templates.privacy.export_failed.html",
        "Templates.privacy.export_ready.html",
        // Czech (cs) — auto-translated, review before production.
        "Templates.Privacy.LegalDocumentObsolete.cs.html",
        "Templates.privacy.deletion_acknowledged.cs.html",
        "Templates.privacy.deletion_cancelled.cs.html",
        "Templates.privacy.deletion_confirmed.cs.html",
        "Templates.privacy.deletion_deferred_confirmed.cs.html",
        "Templates.privacy.deletion_reminder.cs.html",
        "Templates.privacy.export_failed.cs.html",
        "Templates.privacy.export_ready.cs.html",
        // German (de) — auto-translated, review before production.
        "Templates.Privacy.LegalDocumentObsolete.de.html",
        "Templates.privacy.deletion_acknowledged.de.html",
        "Templates.privacy.deletion_cancelled.de.html",
        "Templates.privacy.deletion_confirmed.de.html",
        "Templates.privacy.deletion_deferred_confirmed.de.html",
        "Templates.privacy.deletion_reminder.de.html",
        "Templates.privacy.export_failed.de.html",
        "Templates.privacy.export_ready.de.html",
        // Spanish (es) — auto-translated, review before production.
        "Templates.Privacy.LegalDocumentObsolete.es.html",
        "Templates.privacy.deletion_acknowledged.es.html",
        "Templates.privacy.deletion_cancelled.es.html",
        "Templates.privacy.deletion_confirmed.es.html",
        "Templates.privacy.deletion_deferred_confirmed.es.html",
        "Templates.privacy.deletion_reminder.es.html",
        "Templates.privacy.export_failed.es.html",
        "Templates.privacy.export_ready.es.html",
        // French (fr) — second baseline culture shipped out of the box.
        "Templates.Privacy.LegalDocumentObsolete.fr.html",
        "Templates.privacy.deletion_acknowledged.fr.html",
        "Templates.privacy.deletion_cancelled.fr.html",
        "Templates.privacy.deletion_confirmed.fr.html",
        "Templates.privacy.deletion_deferred_confirmed.fr.html",
        "Templates.privacy.deletion_reminder.fr.html",
        "Templates.privacy.export_failed.fr.html",
        "Templates.privacy.export_ready.fr.html",
        // Hindi (hi) — auto-translated, review before production.
        "Templates.Privacy.LegalDocumentObsolete.hi.html",
        "Templates.privacy.deletion_acknowledged.hi.html",
        "Templates.privacy.deletion_cancelled.hi.html",
        "Templates.privacy.deletion_confirmed.hi.html",
        "Templates.privacy.deletion_deferred_confirmed.hi.html",
        "Templates.privacy.deletion_reminder.hi.html",
        "Templates.privacy.export_failed.hi.html",
        "Templates.privacy.export_ready.hi.html",
        // Italian (it) — auto-translated, review before production.
        "Templates.Privacy.LegalDocumentObsolete.it.html",
        "Templates.privacy.deletion_acknowledged.it.html",
        "Templates.privacy.deletion_cancelled.it.html",
        "Templates.privacy.deletion_confirmed.it.html",
        "Templates.privacy.deletion_deferred_confirmed.it.html",
        "Templates.privacy.deletion_reminder.it.html",
        "Templates.privacy.export_failed.it.html",
        "Templates.privacy.export_ready.it.html",
        // Japanese (ja) — auto-translated, review before production.
        "Templates.Privacy.LegalDocumentObsolete.ja.html",
        "Templates.privacy.deletion_acknowledged.ja.html",
        "Templates.privacy.deletion_cancelled.ja.html",
        "Templates.privacy.deletion_confirmed.ja.html",
        "Templates.privacy.deletion_deferred_confirmed.ja.html",
        "Templates.privacy.deletion_reminder.ja.html",
        "Templates.privacy.export_failed.ja.html",
        "Templates.privacy.export_ready.ja.html",
        // Korean (ko) — auto-translated, review before production.
        "Templates.Privacy.LegalDocumentObsolete.ko.html",
        "Templates.privacy.deletion_acknowledged.ko.html",
        "Templates.privacy.deletion_cancelled.ko.html",
        "Templates.privacy.deletion_confirmed.ko.html",
        "Templates.privacy.deletion_deferred_confirmed.ko.html",
        "Templates.privacy.deletion_reminder.ko.html",
        "Templates.privacy.export_failed.ko.html",
        "Templates.privacy.export_ready.ko.html",
        // Dutch (nl) — auto-translated, review before production.
        "Templates.Privacy.LegalDocumentObsolete.nl.html",
        "Templates.privacy.deletion_acknowledged.nl.html",
        "Templates.privacy.deletion_cancelled.nl.html",
        "Templates.privacy.deletion_confirmed.nl.html",
        "Templates.privacy.deletion_deferred_confirmed.nl.html",
        "Templates.privacy.deletion_reminder.nl.html",
        "Templates.privacy.export_failed.nl.html",
        "Templates.privacy.export_ready.nl.html",
        // Polish (pl) — auto-translated, review before production.
        "Templates.Privacy.LegalDocumentObsolete.pl.html",
        "Templates.privacy.deletion_acknowledged.pl.html",
        "Templates.privacy.deletion_cancelled.pl.html",
        "Templates.privacy.deletion_confirmed.pl.html",
        "Templates.privacy.deletion_deferred_confirmed.pl.html",
        "Templates.privacy.deletion_reminder.pl.html",
        "Templates.privacy.export_failed.pl.html",
        "Templates.privacy.export_ready.pl.html",
        // Portuguese (pt) — auto-translated, review before production.
        "Templates.Privacy.LegalDocumentObsolete.pt.html",
        "Templates.privacy.deletion_acknowledged.pt.html",
        "Templates.privacy.deletion_cancelled.pt.html",
        "Templates.privacy.deletion_confirmed.pt.html",
        "Templates.privacy.deletion_deferred_confirmed.pt.html",
        "Templates.privacy.deletion_reminder.pt.html",
        "Templates.privacy.export_failed.pt.html",
        "Templates.privacy.export_ready.pt.html",
        // Portuguese — Brazil (pt-BR) — LGPD-specific variant, auto-translated, review before production.
        "Templates.Privacy.LegalDocumentObsolete.pt-BR.html",
        "Templates.privacy.deletion_acknowledged.pt-BR.html",
        "Templates.privacy.deletion_cancelled.pt-BR.html",
        "Templates.privacy.deletion_confirmed.pt-BR.html",
        "Templates.privacy.deletion_deferred_confirmed.pt-BR.html",
        "Templates.privacy.deletion_reminder.pt-BR.html",
        "Templates.privacy.export_failed.pt-BR.html",
        "Templates.privacy.export_ready.pt-BR.html",
        // Swedish (sv) — auto-translated, review before production.
        "Templates.Privacy.LegalDocumentObsolete.sv.html",
        "Templates.privacy.deletion_acknowledged.sv.html",
        "Templates.privacy.deletion_cancelled.sv.html",
        "Templates.privacy.deletion_confirmed.sv.html",
        "Templates.privacy.deletion_deferred_confirmed.sv.html",
        "Templates.privacy.deletion_reminder.sv.html",
        "Templates.privacy.export_failed.sv.html",
        "Templates.privacy.export_ready.sv.html",
        // Turkish (tr) — auto-translated, review before production.
        "Templates.Privacy.LegalDocumentObsolete.tr.html",
        "Templates.privacy.deletion_acknowledged.tr.html",
        "Templates.privacy.deletion_cancelled.tr.html",
        "Templates.privacy.deletion_confirmed.tr.html",
        "Templates.privacy.deletion_deferred_confirmed.tr.html",
        "Templates.privacy.deletion_reminder.tr.html",
        "Templates.privacy.export_failed.tr.html",
        "Templates.privacy.export_ready.tr.html",
        // Chinese Simplified (zh) — auto-translated, review before production.
        "Templates.Privacy.LegalDocumentObsolete.zh.html",
        "Templates.privacy.deletion_acknowledged.zh.html",
        "Templates.privacy.deletion_cancelled.zh.html",
        "Templates.privacy.deletion_confirmed.zh.html",
        "Templates.privacy.deletion_deferred_confirmed.zh.html",
        "Templates.privacy.deletion_reminder.zh.html",
        "Templates.privacy.export_failed.zh.html",
        "Templates.privacy.export_ready.zh.html",
    ];

    [Theory]
    [MemberData(nameof(ExpectedTemplates))]
    public void EachExpectedTemplate_IsEmbeddedInTheAssembly(string suffix)
    {
        Assembly assembly = typeof(GranitPrivacyNotificationsModule).Assembly;
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
        Assembly assembly = typeof(GranitPrivacyNotificationsModule).Assembly;
        string fullResourceName = $"{assembly.GetName().Name}.{suffix}";

        using Stream? stream = assembly.GetManifestResourceStream(fullResourceName);
        stream.ShouldNotBeNull($"Resource '{fullResourceName}' should be loadable.");
        stream.Length.ShouldBeGreaterThan(0, $"Resource '{fullResourceName}' should not be empty.");
    }
}
