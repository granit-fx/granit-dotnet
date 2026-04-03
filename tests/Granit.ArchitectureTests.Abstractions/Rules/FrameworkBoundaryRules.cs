using System.Collections.Frozen;
using System.Text.RegularExpressions;
using Shouldly;

namespace Granit.ArchitectureTests.Abstractions.Rules;

/// <summary>
/// Enforces the framework/module boundary: framework packages must never depend on module packages.
/// Dependency flows from modules to framework, never the reverse.
/// </summary>
public static partial class FrameworkBoundaryRules
{
    /// <summary>
    /// The 21 module root prefixes. A project named <c>Granit.{Root}</c> or <c>Granit.{Root}.*</c>
    /// is classified as a <b>module</b>. Everything else under <c>src/Granit.*</c> (except bundles)
    /// is <b>framework</b>.
    /// </summary>
    public static readonly FrozenSet<string> ModuleRootPrefixes = FrozenSet.ToFrozenSet(
    [
        "Auditing",
        "BackgroundJobs",
        "Bff",
        "BlobStorage",
        "DataExchange",
        "DocumentGeneration",
        "Http.Cookies",
        "Identity",
        "Imaging",
        "Invoicing",
        "Notifications",
        "Oidc",
        "OpenIddict",
        "Payments",
        "ReferenceData",
        "Metering",
        "Scheduling",
        "Subscriptions",
        "Tax",
        "Templating",
        "Timeline",
        "Webhooks",
        "Workflow",
    ], StringComparer.Ordinal);

    /// <summary>
    /// Returns <c>true</c> if <paramref name="projectName"/> is a module project
    /// (e.g. <c>Granit.BlobStorage.S3</c>).
    /// </summary>
    public static bool IsModuleProject(string projectName)
    {
        if (!projectName.StartsWith("Granit.", StringComparison.Ordinal))
        {
            return false;
        }

        string suffix = projectName["Granit.".Length..];

        foreach (string root in ModuleRootPrefixes)
        {
            if (string.Equals(suffix, root, StringComparison.Ordinal)
                || suffix.StartsWith(root + ".", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns <c>true</c> if the project path belongs to the <c>bundles/</c> directory.
    /// </summary>
    public static bool IsBundleProject(string csprojRelativePath) =>
        csprojRelativePath.Contains("bundles/", StringComparison.OrdinalIgnoreCase)
        || csprojRelativePath.Contains("bundles\\", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Classifies a project as <c>"framework"</c>, <c>"module"</c>, or <c>"bundle"</c>.
    /// Returns <c>"unknown"</c> if the project does not match any category.
    /// </summary>
    public static string ClassifyProject(string projectName, string csprojRelativePath)
    {
        if (IsBundleProject(csprojRelativePath))
        {
            return "bundle";
        }

        if (!projectName.StartsWith("Granit.", StringComparison.Ordinal)
            && !string.Equals(projectName, "Granit", StringComparison.Ordinal))
        {
            return "unknown";
        }

        return IsModuleProject(projectName) ? "module" : "framework";
    }

    /// <summary>
    /// Asserts that no framework <c>.csproj</c> has a <c>ProjectReference</c> to a module project.
    /// Integration bridges are exempt: a framework project <c>Granit.X.{ModuleRoot}</c> is allowed
    /// to reference module <c>Granit.{ModuleRoot}.*</c> because it is an explicit opt-in bridge.
    /// </summary>
    public static void FrameworkProjectsShouldNotDependOnModules(string repoRoot)
    {
        string srcDir = Path.Join(repoRoot, "src");
        List<string> violations = [];

        foreach (string csproj in Directory.GetFiles(srcDir, "*.csproj", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(repoRoot, csproj).Replace('\\', '/');

            if (IsBundleProject(relativePath))
            {
                continue;
            }

            string projectName = Path.GetFileNameWithoutExtension(csproj);

            if (IsModuleProject(projectName))
            {
                continue;
            }

            // This is a framework project — check its references
            string content = File.ReadAllText(csproj);

            // Strip XML comments to avoid false positives on commented-out references
            content = XmlComment().Replace(content, string.Empty);

            foreach (Match match in ProjectReferenceInclude().Matches(content))
            {
                string refPath = match.Groups[1].Value;
                string refName = Path.GetFileNameWithoutExtension(refPath);

                if (!IsModuleProject(refName))
                {
                    continue;
                }

                // Check integration bridge exemption:
                // Granit.Privacy.BackgroundJobs → Granit.BackgroundJobs is allowed
                // because the framework sub-package name ends with the module root.
                if (IsIntegrationBridgeExempt(projectName, refName))
                {
                    continue;
                }

                violations.Add($"{projectName} → {refName}");
            }
        }

        violations.ShouldBeEmpty(
            "Framework packages must not depend on module packages — dependency flows from " +
            "modules to framework, never the reverse. Integration bridge sub-packages " +
            "(e.g. Granit.Privacy.BackgroundJobs) are exempt. " +
            $"Violations: {string.Join("; ", violations)}");
    }

    /// <summary>
    /// Returns the module root that <paramref name="projectName"/> matches, or <c>null</c>.
    /// </summary>
    internal static string? GetModuleRoot(string projectName)
    {
        if (!projectName.StartsWith("Granit.", StringComparison.Ordinal))
        {
            return null;
        }

        string suffix = projectName["Granit.".Length..];

        foreach (string root in ModuleRootPrefixes)
        {
            if (string.Equals(suffix, root, StringComparison.Ordinal)
                || suffix.StartsWith(root + ".", StringComparison.Ordinal))
            {
                return root;
            }
        }

        return null;
    }

    private static bool IsIntegrationBridgeExempt(string frameworkProject, string moduleProject)
    {
        string? moduleRoot = GetModuleRoot(moduleProject);
        if (moduleRoot is null)
        {
            return false;
        }

        // The framework project name (after Granit.) must end with the module root.
        // E.g. Granit.Privacy.BackgroundJobs ends with "BackgroundJobs".
        string frameworkSuffix = frameworkProject["Granit.".Length..];
        return frameworkSuffix.EndsWith("." + moduleRoot, StringComparison.Ordinal);
    }

    [GeneratedRegex(@"<ProjectReference\s+Include=""([^""]+)""")]
    private static partial Regex ProjectReferenceInclude();

    [GeneratedRegex(@"<!--[\s\S]*?-->")]
    private static partial Regex XmlComment();
}
