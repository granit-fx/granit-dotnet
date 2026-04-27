using System.Reflection;
using Shouldly;
using Xunit;

namespace Granit.Subscriptions.Notifications.Tests.Templates;

/// <summary>
/// Pin the set of embedded templates shipped by the package. A renamed file or a missing
/// `.csproj` glob would break notification rendering at runtime — better to fail here.
/// </summary>
public sealed class EmbeddedTemplatesTests
{
    public static TheoryData<string> ExpectedTemplates() =>
    [
        // Neutral (= EN) variant, one per notification type.
        "Templates.Subscriptions.CancellationConfirmed.html",
        "Templates.Subscriptions.PlanChanged.html",
        "Templates.Subscriptions.ScheduledChangeReminder.html",
        "Templates.Subscriptions.SuspensionWarning.html",
        "Templates.Subscriptions.TrialExpired.html",
        "Templates.Subscriptions.TrialExpiring.html",
        // Czech (cs) — auto-translated, review before production.
        "Templates.Subscriptions.CancellationConfirmed.cs.html",
        "Templates.Subscriptions.PlanChanged.cs.html",
        "Templates.Subscriptions.ScheduledChangeReminder.cs.html",
        "Templates.Subscriptions.SuspensionWarning.cs.html",
        "Templates.Subscriptions.TrialExpired.cs.html",
        "Templates.Subscriptions.TrialExpiring.cs.html",
        // German (de) — auto-translated, review before production.
        "Templates.Subscriptions.CancellationConfirmed.de.html",
        "Templates.Subscriptions.PlanChanged.de.html",
        "Templates.Subscriptions.ScheduledChangeReminder.de.html",
        "Templates.Subscriptions.SuspensionWarning.de.html",
        "Templates.Subscriptions.TrialExpired.de.html",
        "Templates.Subscriptions.TrialExpiring.de.html",
        // British English (en-GB) — auto-translated, review before production.
        "Templates.Subscriptions.CancellationConfirmed.en-GB.html",
        "Templates.Subscriptions.PlanChanged.en-GB.html",
        "Templates.Subscriptions.ScheduledChangeReminder.en-GB.html",
        "Templates.Subscriptions.SuspensionWarning.en-GB.html",
        "Templates.Subscriptions.TrialExpired.en-GB.html",
        "Templates.Subscriptions.TrialExpiring.en-GB.html",
        // Spanish (es) — auto-translated, review before production.
        "Templates.Subscriptions.CancellationConfirmed.es.html",
        "Templates.Subscriptions.PlanChanged.es.html",
        "Templates.Subscriptions.ScheduledChangeReminder.es.html",
        "Templates.Subscriptions.SuspensionWarning.es.html",
        "Templates.Subscriptions.TrialExpired.es.html",
        "Templates.Subscriptions.TrialExpiring.es.html",
        // French (fr) — second baseline culture shipped out of the box.
        "Templates.Subscriptions.CancellationConfirmed.fr.html",
        "Templates.Subscriptions.PlanChanged.fr.html",
        "Templates.Subscriptions.ScheduledChangeReminder.fr.html",
        "Templates.Subscriptions.SuspensionWarning.fr.html",
        "Templates.Subscriptions.TrialExpired.fr.html",
        "Templates.Subscriptions.TrialExpiring.fr.html",
        // Canadian French (fr-CA) — auto-translated, review before production.
        "Templates.Subscriptions.CancellationConfirmed.fr-CA.html",
        "Templates.Subscriptions.PlanChanged.fr-CA.html",
        "Templates.Subscriptions.ScheduledChangeReminder.fr-CA.html",
        "Templates.Subscriptions.SuspensionWarning.fr-CA.html",
        "Templates.Subscriptions.TrialExpired.fr-CA.html",
        "Templates.Subscriptions.TrialExpiring.fr-CA.html",
        // Hindi (hi) — auto-translated, review before production.
        "Templates.Subscriptions.CancellationConfirmed.hi.html",
        "Templates.Subscriptions.PlanChanged.hi.html",
        "Templates.Subscriptions.ScheduledChangeReminder.hi.html",
        "Templates.Subscriptions.SuspensionWarning.hi.html",
        "Templates.Subscriptions.TrialExpired.hi.html",
        "Templates.Subscriptions.TrialExpiring.hi.html",
        // Italian (it) — auto-translated, review before production.
        "Templates.Subscriptions.CancellationConfirmed.it.html",
        "Templates.Subscriptions.PlanChanged.it.html",
        "Templates.Subscriptions.ScheduledChangeReminder.it.html",
        "Templates.Subscriptions.SuspensionWarning.it.html",
        "Templates.Subscriptions.TrialExpired.it.html",
        "Templates.Subscriptions.TrialExpiring.it.html",
        // Japanese (ja) — auto-translated, review before production.
        "Templates.Subscriptions.CancellationConfirmed.ja.html",
        "Templates.Subscriptions.PlanChanged.ja.html",
        "Templates.Subscriptions.ScheduledChangeReminder.ja.html",
        "Templates.Subscriptions.SuspensionWarning.ja.html",
        "Templates.Subscriptions.TrialExpired.ja.html",
        "Templates.Subscriptions.TrialExpiring.ja.html",
        // Korean (ko) — auto-translated, review before production.
        "Templates.Subscriptions.CancellationConfirmed.ko.html",
        "Templates.Subscriptions.PlanChanged.ko.html",
        "Templates.Subscriptions.ScheduledChangeReminder.ko.html",
        "Templates.Subscriptions.SuspensionWarning.ko.html",
        "Templates.Subscriptions.TrialExpired.ko.html",
        "Templates.Subscriptions.TrialExpiring.ko.html",
        // Dutch (nl) — auto-translated, review before production.
        "Templates.Subscriptions.CancellationConfirmed.nl.html",
        "Templates.Subscriptions.PlanChanged.nl.html",
        "Templates.Subscriptions.ScheduledChangeReminder.nl.html",
        "Templates.Subscriptions.SuspensionWarning.nl.html",
        "Templates.Subscriptions.TrialExpired.nl.html",
        "Templates.Subscriptions.TrialExpiring.nl.html",
        // Polish (pl) — auto-translated, review before production.
        "Templates.Subscriptions.CancellationConfirmed.pl.html",
        "Templates.Subscriptions.PlanChanged.pl.html",
        "Templates.Subscriptions.ScheduledChangeReminder.pl.html",
        "Templates.Subscriptions.SuspensionWarning.pl.html",
        "Templates.Subscriptions.TrialExpired.pl.html",
        "Templates.Subscriptions.TrialExpiring.pl.html",
        // Portuguese (pt) — auto-translated, review before production.
        "Templates.Subscriptions.CancellationConfirmed.pt.html",
        "Templates.Subscriptions.PlanChanged.pt.html",
        "Templates.Subscriptions.ScheduledChangeReminder.pt.html",
        "Templates.Subscriptions.SuspensionWarning.pt.html",
        "Templates.Subscriptions.TrialExpired.pt.html",
        "Templates.Subscriptions.TrialExpiring.pt.html",
        // Brazilian Portuguese (pt-BR) — auto-translated, review before production.
        "Templates.Subscriptions.CancellationConfirmed.pt-BR.html",
        "Templates.Subscriptions.PlanChanged.pt-BR.html",
        "Templates.Subscriptions.ScheduledChangeReminder.pt-BR.html",
        "Templates.Subscriptions.SuspensionWarning.pt-BR.html",
        "Templates.Subscriptions.TrialExpired.pt-BR.html",
        "Templates.Subscriptions.TrialExpiring.pt-BR.html",
        // Swedish (sv) — auto-translated, review before production.
        "Templates.Subscriptions.CancellationConfirmed.sv.html",
        "Templates.Subscriptions.PlanChanged.sv.html",
        "Templates.Subscriptions.ScheduledChangeReminder.sv.html",
        "Templates.Subscriptions.SuspensionWarning.sv.html",
        "Templates.Subscriptions.TrialExpired.sv.html",
        "Templates.Subscriptions.TrialExpiring.sv.html",
        // Turkish (tr) — auto-translated, review before production.
        "Templates.Subscriptions.CancellationConfirmed.tr.html",
        "Templates.Subscriptions.PlanChanged.tr.html",
        "Templates.Subscriptions.ScheduledChangeReminder.tr.html",
        "Templates.Subscriptions.SuspensionWarning.tr.html",
        "Templates.Subscriptions.TrialExpired.tr.html",
        "Templates.Subscriptions.TrialExpiring.tr.html",
        // Chinese Simplified (zh) — auto-translated, review before production.
        "Templates.Subscriptions.CancellationConfirmed.zh.html",
        "Templates.Subscriptions.PlanChanged.zh.html",
        "Templates.Subscriptions.ScheduledChangeReminder.zh.html",
        "Templates.Subscriptions.SuspensionWarning.zh.html",
        "Templates.Subscriptions.TrialExpired.zh.html",
        "Templates.Subscriptions.TrialExpiring.zh.html",
    ];

    [Theory]
    [MemberData(nameof(ExpectedTemplates))]
    public void EachExpectedTemplate_IsEmbeddedInTheAssembly(string suffix)
    {
        Assembly assembly = typeof(GranitSubscriptionsNotificationsModule).Assembly;
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
        Assembly assembly = typeof(GranitSubscriptionsNotificationsModule).Assembly;
        string fullResourceName = $"{assembly.GetName().Name}.{suffix}";

        using Stream? stream = assembly.GetManifestResourceStream(fullResourceName);
        stream.ShouldNotBeNull($"Resource '{fullResourceName}' should be loadable.");
        stream.Length.ShouldBeGreaterThan(0, $"Resource '{fullResourceName}' should not be empty.");
    }
}
