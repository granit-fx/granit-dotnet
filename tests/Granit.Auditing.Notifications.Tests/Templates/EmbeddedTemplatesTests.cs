using System.Reflection;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Notifications.Tests.Templates;

/// <summary>
/// Pin the set of embedded templates shipped by the package. A renamed file or a missing
/// `.csproj` glob would break notification rendering at runtime — better to fail here.
/// </summary>
public sealed class EmbeddedTemplatesTests
{
    public static TheoryData<string> ExpectedTemplates() =>
    [
        // Neutral (= EN) variant, one per notification type.
        "Templates.auditing.anomaly_detected.html",
        // French variants — the second culture we ship out of the box.
        "Templates.auditing.anomaly_detected.fr.html",
    ];

    [Theory]
    [MemberData(nameof(ExpectedTemplates))]
    public void EachExpectedTemplate_IsEmbeddedInTheAssembly(string suffix)
    {
        Assembly assembly = typeof(GranitAuditingNotificationsModule).Assembly;
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
        Assembly assembly = typeof(GranitAuditingNotificationsModule).Assembly;
        string fullResourceName = $"{assembly.GetName().Name}.{suffix}";

        using Stream? stream = assembly.GetManifestResourceStream(fullResourceName);
        stream.ShouldNotBeNull($"Resource '{fullResourceName}' should be loadable.");
        stream.Length.ShouldBeGreaterThan(0, $"Resource '{fullResourceName}' should not be empty.");
    }
}
