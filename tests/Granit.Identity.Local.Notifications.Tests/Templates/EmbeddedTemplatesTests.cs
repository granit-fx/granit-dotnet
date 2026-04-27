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
        "Templates.Security.AccountLocked.cs.html",
        "Templates.Security.AccountLocked.de.html",
        "Templates.Security.AccountLocked.es.html",
        "Templates.Security.AccountLocked.fr.html",
        "Templates.Security.AccountLocked.hi.html",
        "Templates.Security.AccountLocked.html",
        "Templates.Security.AccountLocked.it.html",
        "Templates.Security.AccountLocked.ja.html",
        "Templates.Security.AccountLocked.ko.html",
        "Templates.Security.AccountLocked.nl.html",
        "Templates.Security.AccountLocked.pl.html",
        "Templates.Security.AccountLocked.pt-BR.html",
        "Templates.Security.AccountLocked.pt.html",
        "Templates.Security.AccountLocked.sv.html",
        "Templates.Security.AccountLocked.tr.html",
        "Templates.Security.AccountLocked.zh.html",
        "Templates.Security.EmailChangeAlert.cs.html",
        "Templates.Security.EmailChangeAlert.de.html",
        "Templates.Security.EmailChangeAlert.es.html",
        "Templates.Security.EmailChangeAlert.fr-CA.html",
        "Templates.Security.EmailChangeAlert.fr.html",
        "Templates.Security.EmailChangeAlert.hi.html",
        "Templates.Security.EmailChangeAlert.html",
        "Templates.Security.EmailChangeAlert.it.html",
        "Templates.Security.EmailChangeAlert.ja.html",
        "Templates.Security.EmailChangeAlert.ko.html",
        "Templates.Security.EmailChangeAlert.nl.html",
        "Templates.Security.EmailChangeAlert.pl.html",
        "Templates.Security.EmailChangeAlert.pt-BR.html",
        "Templates.Security.EmailChangeAlert.pt.html",
        "Templates.Security.EmailChangeAlert.sv.html",
        "Templates.Security.EmailChangeAlert.tr.html",
        "Templates.Security.EmailChangeAlert.zh.html",
        "Templates.Security.EmailChangeConfirmation.cs.html",
        "Templates.Security.EmailChangeConfirmation.de.html",
        "Templates.Security.EmailChangeConfirmation.es.html",
        "Templates.Security.EmailChangeConfirmation.fr-CA.html",
        "Templates.Security.EmailChangeConfirmation.fr.html",
        "Templates.Security.EmailChangeConfirmation.hi.html",
        "Templates.Security.EmailChangeConfirmation.html",
        "Templates.Security.EmailChangeConfirmation.it.html",
        "Templates.Security.EmailChangeConfirmation.ja.html",
        "Templates.Security.EmailChangeConfirmation.ko.html",
        "Templates.Security.EmailChangeConfirmation.nl.html",
        "Templates.Security.EmailChangeConfirmation.pl.html",
        "Templates.Security.EmailChangeConfirmation.pt-BR.html",
        "Templates.Security.EmailChangeConfirmation.pt.html",
        "Templates.Security.EmailChangeConfirmation.sv.html",
        "Templates.Security.EmailChangeConfirmation.tr.html",
        "Templates.Security.EmailChangeConfirmation.zh.html",
        "Templates.Security.EmailConfirmation.cs.html",
        "Templates.Security.EmailConfirmation.de.html",
        "Templates.Security.EmailConfirmation.es.html",
        "Templates.Security.EmailConfirmation.fr-CA.html",
        "Templates.Security.EmailConfirmation.fr.html",
        "Templates.Security.EmailConfirmation.hi.html",
        "Templates.Security.EmailConfirmation.html",
        "Templates.Security.EmailConfirmation.it.html",
        "Templates.Security.EmailConfirmation.ja.html",
        "Templates.Security.EmailConfirmation.ko.html",
        "Templates.Security.EmailConfirmation.nl.html",
        "Templates.Security.EmailConfirmation.pl.html",
        "Templates.Security.EmailConfirmation.pt-BR.html",
        "Templates.Security.EmailConfirmation.pt.html",
        "Templates.Security.EmailConfirmation.sv.html",
        "Templates.Security.EmailConfirmation.tr.html",
        "Templates.Security.EmailConfirmation.zh.html",
        "Templates.Security.ImpersonationAlert.cs.html",
        "Templates.Security.ImpersonationAlert.de.html",
        "Templates.Security.ImpersonationAlert.es.html",
        "Templates.Security.ImpersonationAlert.fr.html",
        "Templates.Security.ImpersonationAlert.hi.html",
        "Templates.Security.ImpersonationAlert.html",
        "Templates.Security.ImpersonationAlert.it.html",
        "Templates.Security.ImpersonationAlert.ja.html",
        "Templates.Security.ImpersonationAlert.ko.html",
        "Templates.Security.ImpersonationAlert.nl.html",
        "Templates.Security.ImpersonationAlert.pl.html",
        "Templates.Security.ImpersonationAlert.pt-BR.html",
        "Templates.Security.ImpersonationAlert.pt.html",
        "Templates.Security.ImpersonationAlert.sv.html",
        "Templates.Security.ImpersonationAlert.tr.html",
        "Templates.Security.ImpersonationAlert.zh.html",
        "Templates.Security.PasswordChanged.cs.html",
        "Templates.Security.PasswordChanged.de.html",
        "Templates.Security.PasswordChanged.es.html",
        "Templates.Security.PasswordChanged.fr.html",
        "Templates.Security.PasswordChanged.hi.html",
        "Templates.Security.PasswordChanged.html",
        "Templates.Security.PasswordChanged.it.html",
        "Templates.Security.PasswordChanged.ja.html",
        "Templates.Security.PasswordChanged.ko.html",
        "Templates.Security.PasswordChanged.nl.html",
        "Templates.Security.PasswordChanged.pl.html",
        "Templates.Security.PasswordChanged.pt-BR.html",
        "Templates.Security.PasswordChanged.pt.html",
        "Templates.Security.PasswordChanged.sv.html",
        "Templates.Security.PasswordChanged.tr.html",
        "Templates.Security.PasswordChanged.zh.html",
        "Templates.Security.PasswordReset.cs.html",
        "Templates.Security.PasswordReset.de.html",
        "Templates.Security.PasswordReset.es.html",
        "Templates.Security.PasswordReset.fr-CA.html",
        "Templates.Security.PasswordReset.fr.html",
        "Templates.Security.PasswordReset.hi.html",
        "Templates.Security.PasswordReset.html",
        "Templates.Security.PasswordReset.it.html",
        "Templates.Security.PasswordReset.ja.html",
        "Templates.Security.PasswordReset.ko.html",
        "Templates.Security.PasswordReset.nl.html",
        "Templates.Security.PasswordReset.pl.html",
        "Templates.Security.PasswordReset.pt-BR.html",
        "Templates.Security.PasswordReset.pt.html",
        "Templates.Security.PasswordReset.sv.html",
        "Templates.Security.PasswordReset.tr.html",
        "Templates.Security.PasswordReset.zh.html",
        "Templates.Security.TwoFactorChanged.cs.html",
        "Templates.Security.TwoFactorChanged.de.html",
        "Templates.Security.TwoFactorChanged.es.html",
        "Templates.Security.TwoFactorChanged.fr.html",
        "Templates.Security.TwoFactorChanged.hi.html",
        "Templates.Security.TwoFactorChanged.html",
        "Templates.Security.TwoFactorChanged.it.html",
        "Templates.Security.TwoFactorChanged.ja.html",
        "Templates.Security.TwoFactorChanged.ko.html",
        "Templates.Security.TwoFactorChanged.nl.html",
        "Templates.Security.TwoFactorChanged.pl.html",
        "Templates.Security.TwoFactorChanged.pt-BR.html",
        "Templates.Security.TwoFactorChanged.pt.html",
        "Templates.Security.TwoFactorChanged.sv.html",
        "Templates.Security.TwoFactorChanged.tr.html",
        "Templates.Security.TwoFactorChanged.zh.html",
        "Templates.Security.Welcome.cs.html",
        "Templates.Security.Welcome.de.html",
        "Templates.Security.Welcome.es.html",
        "Templates.Security.Welcome.fr-CA.html",
        "Templates.Security.Welcome.fr.html",
        "Templates.Security.Welcome.hi.html",
        "Templates.Security.Welcome.html",
        "Templates.Security.Welcome.it.html",
        "Templates.Security.Welcome.ja.html",
        "Templates.Security.Welcome.ko.html",
        "Templates.Security.Welcome.nl.html",
        "Templates.Security.Welcome.pl.html",
        "Templates.Security.Welcome.pt-BR.html",
        "Templates.Security.Welcome.pt.html",
        "Templates.Security.Welcome.sv.html",
        "Templates.Security.Welcome.tr.html",
        "Templates.Security.Welcome.zh.html",
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
