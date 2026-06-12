using System.Reflection;
using Shouldly;
using Xunit;

namespace Granit.Identity.Notifications.Tests.Templates;

/// <summary>
/// Lighter-weight stand-in for a full MJML render smoke test. Wiring the complete pipeline
/// (Scriban engine + layout + <c>MjmlTransformer</c>) is an integration concern and lives in an
/// integration suite; here we assert the contract the framework's MJML body-detection relies on:
/// the template body — everything after the <c>&lt;title&gt;</c> subject line — begins with an
/// <c>&lt;mj-</c> fragment, so <c>EmailNotificationChannel.StartsWithMjml</c> routes it into the
/// layout raw at the section level instead of wrapping it as plain HTML.
/// </summary>
public sealed class MjmlTemplateBodyTests
{
    [Fact]
    public void SuspiciousSession_En_BodyStartsWithMjmlFragment()
    {
        string body = ReadBodyAfterTitle("Templates.user_sessions.suspicious_session.html");

        body.ShouldStartWith("<mj-", customMessage: "The EN body must be an MJML fragment so the layout injects it raw.");
        body.ShouldContain("<mj-button");
        body.ShouldContain("{{ app.base_url }}/account/security");
    }

    private static string ReadBodyAfterTitle(string suffix)
    {
        Assembly assembly = typeof(GranitIdentityNotificationsModule).Assembly;
        string fullResourceName = $"{assembly.GetName().Name}.{suffix}";

        using Stream? stream = assembly.GetManifestResourceStream(fullResourceName);
        stream.ShouldNotBeNull($"Resource '{fullResourceName}' should be loadable.");

        using var reader = new StreamReader(stream);
        string content = reader.ReadToEnd();

        int titleEnd = content.IndexOf("</title>", StringComparison.OrdinalIgnoreCase);
        titleEnd.ShouldBeGreaterThanOrEqualTo(0, $"'{fullResourceName}' must declare a <title>.");

        return content[(titleEnd + "</title>".Length)..].TrimStart();
    }
}
