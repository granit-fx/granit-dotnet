using System.Reflection;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.Notifications.Tests.Templates;

/// <summary>
/// Pin the set of embedded templates shipped by the package. A renamed file or a missing
/// `.csproj` glob would break notification rendering at runtime — better to fail here.
///
/// 149 templates currently ship: 9 notification types × 15 base cultures (en neutral
/// + 14 translations) + 5 fr-CA + 9 pt-BR variants. The remaining 13 cells of the
/// 9 × 18 culture matrix (9 en-GB + 4 fr-CA) are intentionally omitted because the
/// Scriban culture cascade resolves them via their parent culture (en-GB → en, fr-CA →
/// fr) when there is no Quebec/British-specific divergence to encode.
/// </summary>
public sealed class EmbeddedTemplatesTests
{
    public static TheoryData<string> ExpectedTemplates() =>
    [
        "Templates.identity.account_locked.cs.html",
        "Templates.identity.account_locked.de.html",
        "Templates.identity.account_locked.es.html",
        "Templates.identity.account_locked.fr.html",
        "Templates.identity.account_locked.hi.html",
        "Templates.identity.account_locked.html",
        "Templates.identity.account_locked.it.html",
        "Templates.identity.account_locked.ja.html",
        "Templates.identity.account_locked.ko.html",
        "Templates.identity.account_locked.nl.html",
        "Templates.identity.account_locked.pl.html",
        "Templates.identity.account_locked.pt-BR.html",
        "Templates.identity.account_locked.pt.html",
        "Templates.identity.account_locked.sv.html",
        "Templates.identity.account_locked.tr.html",
        "Templates.identity.account_locked.zh.html",
        "Templates.identity.email_change_alert.cs.html",
        "Templates.identity.email_change_alert.de.html",
        "Templates.identity.email_change_alert.es.html",
        "Templates.identity.email_change_alert.fr-CA.html",
        "Templates.identity.email_change_alert.fr.html",
        "Templates.identity.email_change_alert.hi.html",
        "Templates.identity.email_change_alert.html",
        "Templates.identity.email_change_alert.it.html",
        "Templates.identity.email_change_alert.ja.html",
        "Templates.identity.email_change_alert.ko.html",
        "Templates.identity.email_change_alert.nl.html",
        "Templates.identity.email_change_alert.pl.html",
        "Templates.identity.email_change_alert.pt-BR.html",
        "Templates.identity.email_change_alert.pt.html",
        "Templates.identity.email_change_alert.sv.html",
        "Templates.identity.email_change_alert.tr.html",
        "Templates.identity.email_change_alert.zh.html",
        "Templates.identity.email_change_confirmation.cs.html",
        "Templates.identity.email_change_confirmation.de.html",
        "Templates.identity.email_change_confirmation.es.html",
        "Templates.identity.email_change_confirmation.fr-CA.html",
        "Templates.identity.email_change_confirmation.fr.html",
        "Templates.identity.email_change_confirmation.hi.html",
        "Templates.identity.email_change_confirmation.html",
        "Templates.identity.email_change_confirmation.it.html",
        "Templates.identity.email_change_confirmation.ja.html",
        "Templates.identity.email_change_confirmation.ko.html",
        "Templates.identity.email_change_confirmation.nl.html",
        "Templates.identity.email_change_confirmation.pl.html",
        "Templates.identity.email_change_confirmation.pt-BR.html",
        "Templates.identity.email_change_confirmation.pt.html",
        "Templates.identity.email_change_confirmation.sv.html",
        "Templates.identity.email_change_confirmation.tr.html",
        "Templates.identity.email_change_confirmation.zh.html",
        "Templates.identity.email_confirmation.cs.html",
        "Templates.identity.email_confirmation.de.html",
        "Templates.identity.email_confirmation.es.html",
        "Templates.identity.email_confirmation.fr-CA.html",
        "Templates.identity.email_confirmation.fr.html",
        "Templates.identity.email_confirmation.hi.html",
        "Templates.identity.email_confirmation.html",
        "Templates.identity.email_confirmation.it.html",
        "Templates.identity.email_confirmation.ja.html",
        "Templates.identity.email_confirmation.ko.html",
        "Templates.identity.email_confirmation.nl.html",
        "Templates.identity.email_confirmation.pl.html",
        "Templates.identity.email_confirmation.pt-BR.html",
        "Templates.identity.email_confirmation.pt.html",
        "Templates.identity.email_confirmation.sv.html",
        "Templates.identity.email_confirmation.tr.html",
        "Templates.identity.email_confirmation.zh.html",
        "Templates.identity.impersonation_alert.cs.html",
        "Templates.identity.impersonation_alert.de.html",
        "Templates.identity.impersonation_alert.es.html",
        "Templates.identity.impersonation_alert.fr.html",
        "Templates.identity.impersonation_alert.hi.html",
        "Templates.identity.impersonation_alert.html",
        "Templates.identity.impersonation_alert.it.html",
        "Templates.identity.impersonation_alert.ja.html",
        "Templates.identity.impersonation_alert.ko.html",
        "Templates.identity.impersonation_alert.nl.html",
        "Templates.identity.impersonation_alert.pl.html",
        "Templates.identity.impersonation_alert.pt-BR.html",
        "Templates.identity.impersonation_alert.pt.html",
        "Templates.identity.impersonation_alert.sv.html",
        "Templates.identity.impersonation_alert.tr.html",
        "Templates.identity.impersonation_alert.zh.html",
        "Templates.identity.password_changed.cs.html",
        "Templates.identity.password_changed.de.html",
        "Templates.identity.password_changed.es.html",
        "Templates.identity.password_changed.fr.html",
        "Templates.identity.password_changed.hi.html",
        "Templates.identity.password_changed.html",
        "Templates.identity.password_changed.it.html",
        "Templates.identity.password_changed.ja.html",
        "Templates.identity.password_changed.ko.html",
        "Templates.identity.password_changed.nl.html",
        "Templates.identity.password_changed.pl.html",
        "Templates.identity.password_changed.pt-BR.html",
        "Templates.identity.password_changed.pt.html",
        "Templates.identity.password_changed.sv.html",
        "Templates.identity.password_changed.tr.html",
        "Templates.identity.password_changed.zh.html",
        "Templates.identity.password_reset.cs.html",
        "Templates.identity.password_reset.de.html",
        "Templates.identity.password_reset.es.html",
        "Templates.identity.password_reset.fr-CA.html",
        "Templates.identity.password_reset.fr.html",
        "Templates.identity.password_reset.hi.html",
        "Templates.identity.password_reset.html",
        "Templates.identity.password_reset.it.html",
        "Templates.identity.password_reset.ja.html",
        "Templates.identity.password_reset.ko.html",
        "Templates.identity.password_reset.nl.html",
        "Templates.identity.password_reset.pl.html",
        "Templates.identity.password_reset.pt-BR.html",
        "Templates.identity.password_reset.pt.html",
        "Templates.identity.password_reset.sv.html",
        "Templates.identity.password_reset.tr.html",
        "Templates.identity.password_reset.zh.html",
        "Templates.identity.two_factor_changed.cs.html",
        "Templates.identity.two_factor_changed.de.html",
        "Templates.identity.two_factor_changed.es.html",
        "Templates.identity.two_factor_changed.fr.html",
        "Templates.identity.two_factor_changed.hi.html",
        "Templates.identity.two_factor_changed.html",
        "Templates.identity.two_factor_changed.it.html",
        "Templates.identity.two_factor_changed.ja.html",
        "Templates.identity.two_factor_changed.ko.html",
        "Templates.identity.two_factor_changed.nl.html",
        "Templates.identity.two_factor_changed.pl.html",
        "Templates.identity.two_factor_changed.pt-BR.html",
        "Templates.identity.two_factor_changed.pt.html",
        "Templates.identity.two_factor_changed.sv.html",
        "Templates.identity.two_factor_changed.tr.html",
        "Templates.identity.two_factor_changed.zh.html",
        "Templates.identity.welcome.cs.html",
        "Templates.identity.welcome.de.html",
        "Templates.identity.welcome.es.html",
        "Templates.identity.welcome.fr-CA.html",
        "Templates.identity.welcome.fr.html",
        "Templates.identity.welcome.hi.html",
        "Templates.identity.welcome.html",
        "Templates.identity.welcome.it.html",
        "Templates.identity.welcome.ja.html",
        "Templates.identity.welcome.ko.html",
        "Templates.identity.welcome.nl.html",
        "Templates.identity.welcome.pl.html",
        "Templates.identity.welcome.pt-BR.html",
        "Templates.identity.welcome.pt.html",
        "Templates.identity.welcome.sv.html",
        "Templates.identity.welcome.tr.html",
        "Templates.identity.welcome.zh.html",
    ];

    [Theory]
    [MemberData(nameof(ExpectedTemplates))]
    public void EachExpectedTemplate_IsEmbeddedInTheAssembly(string suffix)
    {
        Assembly assembly = typeof(GranitIdentityLocalNotificationsModule).Assembly;
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
        Assembly assembly = typeof(GranitIdentityLocalNotificationsModule).Assembly;
        string fullResourceName = $"{assembly.GetName().Name}.{suffix}";

        using Stream? stream = assembly.GetManifestResourceStream(fullResourceName);
        stream.ShouldNotBeNull($"Resource '{fullResourceName}' should be loadable.");
        stream.Length.ShouldBeGreaterThan(0, $"Resource '{fullResourceName}' should not be empty.");
    }
}
