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
        "Templates.privacy.deletion_acknowledged.html",
        "Templates.privacy.deletion_reminder.html",
        "Templates.privacy.deletion_deferred_confirmed.html",
        "Templates.privacy.deletion_cancelled.html",
        "Templates.privacy.deletion_confirmed.html",
        "Templates.privacy.export_ready.html",
        "Templates.privacy.export_failed.html",
        "Templates.Privacy.LegalDocumentObsolete.html",
        // French variants — the second culture we ship out of the box.
        "Templates.privacy.deletion_acknowledged.fr.html",
        "Templates.privacy.deletion_reminder.fr.html",
        "Templates.privacy.deletion_deferred_confirmed.fr.html",
        "Templates.privacy.deletion_cancelled.fr.html",
        "Templates.privacy.deletion_confirmed.fr.html",
        "Templates.privacy.export_ready.fr.html",
        "Templates.privacy.export_failed.fr.html",
        "Templates.Privacy.LegalDocumentObsolete.fr.html",
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
