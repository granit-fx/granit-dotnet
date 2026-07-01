using System.Reflection;
using Shouldly;
using Xunit;

namespace Granit.Hostnames.Notifications.Tests.Templates;

/// <summary>
/// Pin the set of embedded templates shipped by the package. A renamed file or a missing
/// .csproj glob would break notification rendering at runtime — better to fail here.
/// </summary>
public sealed class EmbeddedTemplatesTests
{
    public static TheoryData<string> ExpectedTemplates() =>
    [
        // ── hostnames.hostname_verified ──────────────────────────────────────
        // Neutral (= EN) variant.
        "Templates.hostnames.hostname_verified.html",
        // French — second baseline culture shipped out of the box.
        "Templates.hostnames.hostname_verified.fr.html",
        // Auto-translated cultures — review before production.
        "Templates.hostnames.hostname_verified.cs.html",
        "Templates.hostnames.hostname_verified.de.html",
        "Templates.hostnames.hostname_verified.en-GB.html",
        "Templates.hostnames.hostname_verified.es.html",
        "Templates.hostnames.hostname_verified.fr-CA.html",
        "Templates.hostnames.hostname_verified.hi.html",
        "Templates.hostnames.hostname_verified.it.html",
        "Templates.hostnames.hostname_verified.ja.html",
        "Templates.hostnames.hostname_verified.ko.html",
        "Templates.hostnames.hostname_verified.nl.html",
        "Templates.hostnames.hostname_verified.pl.html",
        "Templates.hostnames.hostname_verified.pt-BR.html",
        "Templates.hostnames.hostname_verified.pt.html",
        "Templates.hostnames.hostname_verified.sv.html",
        "Templates.hostnames.hostname_verified.tr.html",
        "Templates.hostnames.hostname_verified.zh.html",
        // ── hostnames.hostname_verification_failed ───────────────────────────
        "Templates.hostnames.hostname_verification_failed.html",
        "Templates.hostnames.hostname_verification_failed.fr.html",
        "Templates.hostnames.hostname_verification_failed.cs.html",
        "Templates.hostnames.hostname_verification_failed.de.html",
        "Templates.hostnames.hostname_verification_failed.en-GB.html",
        "Templates.hostnames.hostname_verification_failed.es.html",
        "Templates.hostnames.hostname_verification_failed.fr-CA.html",
        "Templates.hostnames.hostname_verification_failed.hi.html",
        "Templates.hostnames.hostname_verification_failed.it.html",
        "Templates.hostnames.hostname_verification_failed.ja.html",
        "Templates.hostnames.hostname_verification_failed.ko.html",
        "Templates.hostnames.hostname_verification_failed.nl.html",
        "Templates.hostnames.hostname_verification_failed.pl.html",
        "Templates.hostnames.hostname_verification_failed.pt-BR.html",
        "Templates.hostnames.hostname_verification_failed.pt.html",
        "Templates.hostnames.hostname_verification_failed.sv.html",
        "Templates.hostnames.hostname_verification_failed.tr.html",
        "Templates.hostnames.hostname_verification_failed.zh.html",
        // ── hostnames.certificate_secured ────────────────────────────────────
        // Neutral (= EN) variant.
        "Templates.hostnames.certificate_secured.html",
        // French — second baseline culture shipped out of the box.
        "Templates.hostnames.certificate_secured.fr.html",
        // Auto-translated cultures — review before production.
        "Templates.hostnames.certificate_secured.cs.html",
        "Templates.hostnames.certificate_secured.de.html",
        "Templates.hostnames.certificate_secured.en-GB.html",
        "Templates.hostnames.certificate_secured.es.html",
        "Templates.hostnames.certificate_secured.fr-CA.html",
        "Templates.hostnames.certificate_secured.hi.html",
        "Templates.hostnames.certificate_secured.it.html",
        "Templates.hostnames.certificate_secured.ja.html",
        "Templates.hostnames.certificate_secured.ko.html",
        "Templates.hostnames.certificate_secured.nl.html",
        "Templates.hostnames.certificate_secured.pl.html",
        "Templates.hostnames.certificate_secured.pt-BR.html",
        "Templates.hostnames.certificate_secured.pt.html",
        "Templates.hostnames.certificate_secured.sv.html",
        "Templates.hostnames.certificate_secured.tr.html",
        "Templates.hostnames.certificate_secured.zh.html",
        // ── hostnames.certificate_failed ──────────────────────────────────────
        "Templates.hostnames.certificate_failed.html",
        "Templates.hostnames.certificate_failed.fr.html",
        "Templates.hostnames.certificate_failed.cs.html",
        "Templates.hostnames.certificate_failed.de.html",
        "Templates.hostnames.certificate_failed.en-GB.html",
        "Templates.hostnames.certificate_failed.es.html",
        "Templates.hostnames.certificate_failed.fr-CA.html",
        "Templates.hostnames.certificate_failed.hi.html",
        "Templates.hostnames.certificate_failed.it.html",
        "Templates.hostnames.certificate_failed.ja.html",
        "Templates.hostnames.certificate_failed.ko.html",
        "Templates.hostnames.certificate_failed.nl.html",
        "Templates.hostnames.certificate_failed.pl.html",
        "Templates.hostnames.certificate_failed.pt-BR.html",
        "Templates.hostnames.certificate_failed.pt.html",
        "Templates.hostnames.certificate_failed.sv.html",
        "Templates.hostnames.certificate_failed.tr.html",
        "Templates.hostnames.certificate_failed.zh.html",
    ];

    [Theory]
    [MemberData(nameof(ExpectedTemplates))]
    public void EachExpectedTemplate_IsEmbeddedInTheAssembly(string suffix)
    {
        Assembly assembly = typeof(GranitHostnamesNotificationsModule).Assembly;
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
        Assembly assembly = typeof(GranitHostnamesNotificationsModule).Assembly;
        string fullResourceName = $"{assembly.GetName().Name}.{suffix}";

        using Stream? stream = assembly.GetManifestResourceStream(fullResourceName);
        stream.ShouldNotBeNull($"Resource '{fullResourceName}' should be loadable.");
        stream.Length.ShouldBeGreaterThan(0, $"Resource '{fullResourceName}' should not be empty.");
    }
}
