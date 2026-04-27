using System.Reflection;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Notifications.Tests.Templates;

/// <summary>
/// Pin the set of embedded templates shipped by the package. A renamed file or a missing
/// `.csproj` glob would break notification rendering at runtime — better to fail here.
/// </summary>
public sealed class EmbeddedTemplatesTests
{
    public static TheoryData<string> ExpectedTemplates() =>
    [
        // Neutral (= EN) variants, one per notification type.
        "Templates.identity.token_exchange_audit.html",
        "Templates.identity.user_provisioning_removed.html",
        // French variants — the second culture we ship out of the box.
        "Templates.identity.token_exchange_audit.fr.html",
        "Templates.identity.user_provisioning_removed.fr.html",
    ];

    [Theory]
    [MemberData(nameof(ExpectedTemplates))]
    public void EachExpectedTemplate_IsEmbeddedInTheAssembly(string suffix)
    {
        Assembly assembly = typeof(GranitIdentityFederatedNotificationsModule).Assembly;
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
        Assembly assembly = typeof(GranitIdentityFederatedNotificationsModule).Assembly;
        string fullResourceName = $"{assembly.GetName().Name}.{suffix}";

        using Stream? stream = assembly.GetManifestResourceStream(fullResourceName);
        stream.ShouldNotBeNull($"Resource '{fullResourceName}' should be loadable.");
        stream.Length.ShouldBeGreaterThan(0, $"Resource '{fullResourceName}' should not be empty.");
    }
}
