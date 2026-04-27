using System.Reflection;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.Notifications.Tests.Templates;

/// <summary>
/// Pin the set of embedded templates shipped by the package. A renamed file or a missing
/// `.csproj` glob would break notification rendering at runtime — better to fail here.
/// </summary>
public sealed class EmbeddedTemplatesTests
{
    public static TheoryData<string> ExpectedTemplates() =>
    [
        // Neutral (= EN) variant, one per notification type.
        "Templates.apikeys.new_key_issued.html",
        "Templates.apikeys.rotation_completed.html",
        "Templates.apikeys.revoked.html",
        "Templates.apikeys.expiring_soon.html",
        // French variants — the second culture we ship out of the box.
        "Templates.apikeys.new_key_issued.fr.html",
        "Templates.apikeys.rotation_completed.fr.html",
        "Templates.apikeys.revoked.fr.html",
        "Templates.apikeys.expiring_soon.fr.html",
    ];

    [Theory]
    [MemberData(nameof(ExpectedTemplates))]
    public void EachExpectedTemplate_IsEmbeddedInTheAssembly(string suffix)
    {
        Assembly assembly = typeof(GranitApiKeysNotificationsModule).Assembly;
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
        Assembly assembly = typeof(GranitApiKeysNotificationsModule).Assembly;
        string fullResourceName = $"{assembly.GetName().Name}.{suffix}";

        using Stream? stream = assembly.GetManifestResourceStream(fullResourceName);
        stream.ShouldNotBeNull($"Resource '{fullResourceName}' should be loadable.");
        stream.Length.ShouldBeGreaterThan(0, $"Resource '{fullResourceName}' should not be empty.");
    }

    /// <summary>
    /// Defence in depth — scan every embedded template for any token that could indicate
    /// a leak of secret material (e.g. <c>{{ model.value }}</c>, <c>{{ model.secret }}</c>,
    /// <c>{{ model.hash }}</c>). Even if a future refactor accidentally introduces such
    /// a field on the data record, this test fails before the email is rendered.
    /// </summary>
    [Theory]
    [MemberData(nameof(ExpectedTemplates))]
    public void NoTemplate_ReferencesAnySecretField(string suffix)
    {
        Assembly assembly = typeof(GranitApiKeysNotificationsModule).Assembly;
        string fullResourceName = $"{assembly.GetName().Name}.{suffix}";

        using Stream? stream = assembly.GetManifestResourceStream(fullResourceName);
        stream.ShouldNotBeNull();
        using StreamReader reader = new(stream);
        string content = reader.ReadToEnd();

        string[] forbidden =
        [
            "model.value",
            "model.secret",
            "model.hash",
            "model.hashed_key",
            "model.raw_key",
        ];
        foreach (string token in forbidden)
        {
            content.ShouldNotContain(
                token,
                Case.Insensitive,
                customMessage: $"Template '{fullResourceName}' references forbidden secret-bearing field '{token}'.");
        }
    }
}
