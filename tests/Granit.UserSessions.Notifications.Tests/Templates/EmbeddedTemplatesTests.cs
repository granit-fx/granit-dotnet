using System.Reflection;
using Shouldly;
using Xunit;

namespace Granit.UserSessions.Notifications.Tests.Templates;

/// <summary>
/// Pin the set of embedded templates shipped by the package. A renamed file or a missing
/// `.csproj` glob would break notification rendering at runtime — better to fail here.
/// Only the EN baseline and FR are shipped out of the box; the other 16 cultures are
/// generated later by <c>scripts/translate-templates.py</c>.
/// </summary>
public sealed class EmbeddedTemplatesTests
{
    public static TheoryData<string> ExpectedTemplates() =>
    [
        // Neutral (= EN) variant, one per notification type.
        "Templates.user_sessions.suspicious_session.html",
        "Templates.user_sessions.new_session_review.html",
        // French (fr) — second baseline culture shipped out of the box.
        "Templates.user_sessions.suspicious_session.fr.html",
        "Templates.user_sessions.new_session_review.fr.html",
    ];

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
    public void EachExpectedTemplate_FirstLineIsTitle(string suffix)
    {
        Assembly assembly = typeof(GranitUserSessionsNotificationsModule).Assembly;
        string fullResourceName = $"{assembly.GetName().Name}.{suffix}";

        using Stream? stream = assembly.GetManifestResourceStream(fullResourceName);
        stream.ShouldNotBeNull($"Resource '{fullResourceName}' should be loadable.");

        using var reader = new StreamReader(stream);
        string content = reader.ReadToEnd();
        content.Length.ShouldBeGreaterThan(0, $"Resource '{fullResourceName}' should not be empty.");

        // The email channel extracts the subject from the first line, which MUST be a <title>.
        string firstLine = content.Split('\n', 2)[0].Trim();
        firstLine.ShouldStartWith("<title>", customMessage: $"First line of '{fullResourceName}' must be a <title> subject.");
        firstLine.ShouldEndWith("</title>", customMessage: $"First line of '{fullResourceName}' must be a complete <title> element.");
    }
}
