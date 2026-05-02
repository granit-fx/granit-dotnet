using System.Reflection;
using Shouldly;
using Xunit;

namespace Granit.Activities.Notifications.Tests;

/// <summary>
/// Pin the set of embedded templates shipped by the package — a renamed file
/// or a missing <c>EmbeddedResource</c> glob would break notification
/// rendering at runtime, better to fail here.
/// </summary>
public sealed class EmbeddedTemplatesTests
{
    public static TheoryData<string> ExpectedTemplates() =>
    [
        // Neutral (= EN) variant, one per notification type.
        "Templates.activity.assigned.html",
        "Templates.activity.reminder.html",
        "Templates.activity.overdue.html",
        // French baseline.
        "Templates.activity.assigned.fr.html",
        "Templates.activity.reminder.fr.html",
        "Templates.activity.overdue.fr.html",
    ];

    [Theory]
    [MemberData(nameof(ExpectedTemplates))]
    public void EachExpectedTemplate_IsEmbeddedInTheAssembly(string suffix)
    {
        Assembly assembly = typeof(GranitActivitiesNotificationsModule).Assembly;
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
        Assembly assembly = typeof(GranitActivitiesNotificationsModule).Assembly;
        string fullResourceName = $"{assembly.GetName().Name}.{suffix}";

        using Stream? stream = assembly.GetManifestResourceStream(fullResourceName);
        stream.ShouldNotBeNull($"Resource '{fullResourceName}' should be loadable.");
        stream.Length.ShouldBeGreaterThan(0, $"Resource '{fullResourceName}' should not be empty.");
    }

    [Theory]
    [MemberData(nameof(ExpectedTemplates))]
    public void Each_template_starts_with_a_title_tag_for_email_subject_extraction(string suffix)
    {
        Assembly assembly = typeof(GranitActivitiesNotificationsModule).Assembly;
        using Stream stream = assembly.GetManifestResourceStream($"{assembly.GetName().Name}.{suffix}")!;
        using StreamReader reader = new(stream);
        string firstLine = reader.ReadLine() ?? string.Empty;
        firstLine.ShouldStartWith("<title>");
    }
}
