using System.Reflection;
using Shouldly;
using Xunit;

namespace Granit.UserSessions.Notifications.Tests.Templates;

/// <summary>
/// Pin the set of embedded templates shipped by the package. A renamed file or a missing
/// `.csproj` glob would break notification rendering at runtime — better to fail here.
/// Both notification types ship in all 16 cultures (the EN baseline plus 15 suffixed
/// variants); the suffixed files carry an <c>&lt;!-- AUTO-TRANSLATED --&gt;</c> marker and
/// must be reviewed before production.
/// </summary>
public sealed class EmbeddedTemplatesTests
{
    private static readonly string[] NotificationNames =
    [
        "user_sessions.suspicious_session",
        "user_sessions.new_session_review",
    ];

    // Neutral (= EN) baseline ("") plus the 15 suffixed cultures.
    private static readonly string[] CultureSuffixes =
    [
        "",
        "cs", "de", "es", "fr", "hi", "it", "ja", "ko",
        "nl", "pl", "pt-BR", "pt", "sv", "tr", "zh",
    ];

    public static TheoryData<string> ExpectedTemplates()
    {
        TheoryData<string> data = [];
        foreach (string name in NotificationNames)
        {
            foreach (string culture in CultureSuffixes)
            {
                string variant = culture.Length == 0 ? "" : $".{culture}";
                data.Add($"Templates.{name}{variant}.html");
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ExpectedTemplates))]
    public void EachExpectedTemplate_IsEmbeddedInTheAssembly(string suffix)
    {
        Assembly assembly = typeof(GranitUserSessionsNotificationsModule).Assembly;
        string assemblyName = assembly.GetName().Name!;
        string fullResourceName = $"{assemblyName}.{suffix}";

        string[] resources = assembly.GetManifestResourceNames();

        resources.ShouldContain(
            fullResourceName,
            customMessage: $"Embedded resource '{fullResourceName}' is missing. Check the <EmbeddedResource> glob in the .csproj and the file presence under Templates/.");
    }

    [Theory]
    [MemberData(nameof(ExpectedTemplates))]
    public void EachExpectedTemplate_ContainsTitle(string suffix)
    {
        Assembly assembly = typeof(GranitUserSessionsNotificationsModule).Assembly;
        string fullResourceName = $"{assembly.GetName().Name}.{suffix}";

        using Stream? stream = assembly.GetManifestResourceStream(fullResourceName);
        stream.ShouldNotBeNull($"Resource '{fullResourceName}' should be loadable.");

        using var reader = new StreamReader(stream);
        string content = reader.ReadToEnd();
        content.Length.ShouldBeGreaterThan(0, $"Resource '{fullResourceName}' should not be empty.");

        // The email channel extracts the subject from the <title>. On the EN baseline it is
        // the first line; on translated variants it follows the AUTO-TRANSLATED marker line.
        content.ShouldContain("<title>", customMessage: $"'{fullResourceName}' must declare a <title> subject.");
        content.ShouldContain("</title>", customMessage: $"'{fullResourceName}' must close its <title> element.");
    }
}
