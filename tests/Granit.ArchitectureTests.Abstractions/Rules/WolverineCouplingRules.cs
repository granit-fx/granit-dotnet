using Shouldly;

namespace Granit.ArchitectureTests.Abstractions.Rules;

/// <summary>
/// Reusable rules guarding against direct Wolverine coupling in domain packages.
/// Only designated adapter projects may depend on Wolverine directly.
/// </summary>
public static class WolverineCouplingRules
{
    /// <summary>
    /// Source files in non-adapter packages must not contain <c>using Wolverine;</c>
    /// or <c>using Wolverine.*;</c> directives.
    /// </summary>
    /// <param name="srcDir">Path to the repo's <c>src/</c> directory.</param>
    /// <param name="repoRoot">Repo root for relative path messages.</param>
    /// <param name="allowedProjectNames">
    /// Directory names (under <c>src/</c>) whose Wolverine coupling is legitimate
    /// (e.g. <c>"Granit.Wolverine"</c>, <c>"Granit.Events.Wolverine"</c>).
    /// </param>
    public static void DomainPackagesShouldNotUseWolverineNamespace(
        string srcDir,
        string repoRoot,
        IReadOnlySet<string> allowedProjectNames)
    {
        List<string> violations = [];

        foreach (string csFile in EnumerateSrcCsFiles(srcDir))
        {
            string projectName = GetProjectDirectoryName(srcDir, csFile);
            if (allowedProjectNames.Contains(projectName))
            {
                continue;
            }

            if (HasWolverineUsing(File.ReadAllText(csFile)))
            {
                violations.Add($"{Path.GetRelativePath(repoRoot, csFile)}: contains 'using Wolverine...'");
            }
        }

        violations.ShouldBeEmpty(
            "Domain (non-adapter) packages must not reference Wolverine types directly. " +
            "Use ICommandSender from Granit and rely on handler convention discovery. " +
            $"Violations:{Environment.NewLine} - {string.Join(Environment.NewLine + " - ", violations)}");
    }

    /// <summary>
    /// Non-adapter project files must not declare a <c>PackageReference</c> to <c>WolverineFx</c>.
    /// </summary>
    public static void DomainPackagesShouldNotReferenceWolverineFxPackage(
        string srcDir,
        string repoRoot,
        IReadOnlySet<string> allowedProjectNames)
    {
        List<string> violations = [];

        foreach (string csproj in Directory.EnumerateFiles(srcDir, "*.csproj", SearchOption.AllDirectories))
        {
            string projectName = Path.GetFileNameWithoutExtension(csproj);
            if (allowedProjectNames.Contains(projectName))
            {
                continue;
            }

            if (File.ReadAllText(csproj).Contains("Include=\"WolverineFx\"", StringComparison.Ordinal))
            {
                violations.Add($"{Path.GetRelativePath(repoRoot, csproj)}: references WolverineFx");
            }
        }

        violations.ShouldBeEmpty(
            "Only adapter packages may reference the WolverineFx NuGet package. " +
            $"Violations:{Environment.NewLine} - {string.Join(Environment.NewLine + " - ", violations)}");
    }

    private static bool HasWolverineUsing(string content)
    {
        foreach (string trimmed in content.Split('\n').Select(static l => l.TrimStart()))
        {
            if (trimmed.StartsWith("using Wolverine;", StringComparison.Ordinal)
                || trimmed.StartsWith("using Wolverine.", StringComparison.Ordinal)
                || trimmed.StartsWith("using static Wolverine", StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static string GetProjectDirectoryName(string srcDir, string csFile)
    {
        string relative = Path.GetRelativePath(srcDir, csFile).Replace('\\', '/');
        int firstSep = relative.IndexOf('/');
        return firstSep > 0 ? relative[..firstSep] : relative;
    }

    private static IEnumerable<string> EnumerateSrcCsFiles(string srcDir) =>
        Directory.EnumerateFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Replace('\\', '/').Contains("/bin/", StringComparison.Ordinal)
                     && !f.Replace('\\', '/').Contains("/obj/", StringComparison.Ordinal));
}
