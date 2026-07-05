using ArchUnitNET.Domain;
using Shouldly;

namespace Granit.ArchitectureTests.Abstractions.Rules;

/// <summary>
/// Enforces background job naming conventions:
/// <list type="bullet">
///   <item><c>IBackgroundJob</c> implementors must end with <c>Job</c></item>
///   <item>Types with <c>[RecurringJob]</c> must implement <c>IBackgroundJob</c></item>
/// </list>
/// </summary>
public static class BackgroundJobConventionRules
{
    /// <summary>
    /// Every <c>IBackgroundJob</c> implementor must have a name ending with <c>Job</c>.
    /// </summary>
    public static void BackgroundJobsMustEndWithJob(
        Architecture architecture,
        string typePrefix)
    {
        var violations = architecture.Classes
            .Where(c => c.FullName.StartsWith(typePrefix, StringComparison.Ordinal)
                && !c.IsAbstract.GetValueOrDefault()
                && ImplementsInterface(c, "Granit.BackgroundJobs.IBackgroundJob") && !StripGenericArity(c.Name).EndsWith("Job", StringComparison.Ordinal))
            .Select(c => $"{c.FullName} (IBackgroundJob must end with 'Job')")
            .ToList();

        violations.ShouldBeEmpty(
            "Background job types must end with 'Job' suffix. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    /// <summary>
    /// Every type decorated with <c>[RecurringJob]</c> must implement <c>IBackgroundJob</c>.
    /// </summary>
    public static void RecurringJobsMustImplementIBackgroundJob(
        Architecture architecture,
        string typePrefix)
    {
        var violations = architecture.Classes
            .Where(c => c.FullName.StartsWith(typePrefix, StringComparison.Ordinal)
                && !c.IsAbstract.GetValueOrDefault()
                && HasAttribute(c, "Granit.BackgroundJobs.RecurringJobAttribute") && !ImplementsInterface(c, "Granit.BackgroundJobs.IBackgroundJob"))
            .Select(c => $"{c.FullName} (has [RecurringJob] but does not implement IBackgroundJob)")
            .ToList();

        violations.ShouldBeEmpty(
            "Types with [RecurringJob] must implement IBackgroundJob. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    // ArchUnitNET reports generic type names with a CLR arity suffix (e.g. "RebuildIndexJob`1");
    // strip it so generic jobs are matched on their declared name.
    private static string StripGenericArity(string name)
    {
        int backtick = name.IndexOf('`', StringComparison.Ordinal);
        return backtick < 0 ? name : name[..backtick];
    }

    private static bool ImplementsInterface(Class c, string interfaceFullName) =>
        c.Dependencies.Any(d =>
            d.Target.FullName == interfaceFullName
            && d is ArchUnitNET.Domain.Dependencies.ImplementsInterfaceDependency);

    private static bool HasAttribute(Class c, string attributeFullName) =>
        c.Attributes.Any(a => a.FullName == attributeFullName);
}
