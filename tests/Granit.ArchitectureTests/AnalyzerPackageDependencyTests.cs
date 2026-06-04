using System.Xml.Linq;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Guards against analyzer / source-generator packages leaking compile-time dependencies
/// (Roslyn, System.Composition, …) into consumer package graphs.
/// </summary>
/// <remarks>
/// <para>
/// Analyzer and source-generator packages run inside the host compiler, which already supplies
/// <c>Microsoft.CodeAnalysis.*</c>. They ship their DLLs under <c>analyzers/</c> and must declare
/// <b>zero</b> NuGet dependencies: every <c>&lt;PackageReference&gt;</c> carries
/// <c>PrivateAssets="all"</c> so it never reaches the produced <c>.nuspec</c>.
/// </para>
/// <para>
/// Regression this prevents: <c>Granit.Analyzers</c> dev.3360–3368 dropped <c>PrivateAssets="all"</c>
/// from <c>Microsoft.CodeAnalysis.CSharp</c>, so the <c>VersionOverride="5.3.0"</c> leaked into the
/// nuspec and pulled <c>Microsoft.CodeAnalysis.Common 5.3.0</c> into every consumer's app graph —
/// colliding with Wolverine/JasperFx's exact <c>[5.0.0]</c> constraint → <c>NU1608</c> "warning as
/// error" on every project referencing <c>Granit.Wolverine</c> (broke granit-business). Note:
/// <c>&lt;DevelopmentDependency&gt;true&lt;/DevelopmentDependency&gt;</c> alone does NOT suppress the
/// dependency — <c>PrivateAssets="all"</c> on the reference does.
/// </para>
/// <para>
/// Mechanism: a static csproj check rather than pack-and-inspect-nuspec. It catches the exact
/// regression (a dropped <c>PrivateAssets</c>), runs offline and deterministically, and matches the
/// existing csproj-scanning ArchitectureTests style. The packed-artifact guarantee (empty
/// <c>&lt;dependencies&gt;</c> in the real <c>.nuspec</c>) is verified manually when these packages
/// change — pack-at-test-time was rejected: no precedent in the suite, and it requires a fragile
/// build ordering (<c>Granit.Analyzers</c> packs the pre-built <c>Granit.Analyzers.CodeFixes</c> DLL).
/// </para>
/// </remarks>
public sealed class AnalyzerPackageDependencyTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void Analyzer_and_source_generator_packages_must_keep_PrivateAssets_all_on_every_PackageReference()
    {
        IReadOnlyList<string> projects = DiscoverPackableAnalyzerProjects();

        // Anti-vacuous guard: if discovery breaks, the test must fail loudly rather than pass empty.
        IEnumerable<string> names = projects.Select(p => Path.GetFileNameWithoutExtension(p)!);
        names.ShouldContain("Granit.Analyzers",
            "Discovery of packable analyzer packages is broken — expected Granit.Analyzers.");
        names.ShouldContain("Granit.Localization.SourceGenerator",
            "Discovery of packable analyzer packages is broken — expected Granit.Localization.SourceGenerator.");

        List<string> violations = [];

        foreach (string csproj in projects)
        {
            string packageName = Path.GetFileNameWithoutExtension(csproj);
            var doc = XDocument.Load(csproj);

            foreach (XElement reference in doc.Descendants("PackageReference"))
            {
                string? id = (string?)reference.Attribute("Include") ?? (string?)reference.Attribute("Update");
                if (id is null)
                {
                    continue;
                }

                if (!HasPrivateAssetsAll(reference))
                {
                    violations.Add($"{packageName}: PackageReference '{id}' is missing PrivateAssets=\"all\"");
                }
            }
        }

        violations.ShouldBeEmpty(
            "Analyzer/source-generator packages must declare PrivateAssets=\"all\" on EVERY PackageReference so the " +
            "packed .nuspec carries zero dependencies. A leaking reference drags compile-time Roslyn into consumer " +
            "package graphs and triggers NU1608 against Wolverine/JasperFx's pinned Microsoft.CodeAnalysis versions.\n"
            + string.Join("\n", violations));
    }

    /// <summary>
    /// True when the reference declares <c>PrivateAssets="all"</c> in either the attribute form
    /// (<c>PrivateAssets="all"</c>) or the child-element form
    /// (<c>&lt;PrivateAssets&gt;all&lt;/PrivateAssets&gt;</c>). Case-insensitive.
    /// </summary>
    private static bool HasPrivateAssetsAll(XElement reference)
    {
        string? attribute = (string?)reference.Attribute("PrivateAssets");
        if (string.Equals(attribute, "all", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        XElement? element = reference.Elements("PrivateAssets").FirstOrDefault();
        return string.Equals(element?.Value.Trim(), "all", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Discovers packable analyzer / source-generator projects under <c>src/</c>: those marked both
    /// <c>IsPackable=true</c> and <c>DevelopmentDependency=true</c> — the convention for packages
    /// whose DLLs ship under <c>analyzers/</c> and must carry no NuGet dependencies. Enumerated
    /// dynamically so any future analyzer/source-gen package is covered automatically.
    /// </summary>
    private static IReadOnlyList<string> DiscoverPackableAnalyzerProjects()
    {
        string srcDir = Path.Join(RepoRoot, "src");

        return
        [
            .. Directory.GetFiles(srcDir, "*.csproj", SearchOption.AllDirectories)
                .Where(csproj =>
                {
                    string content = File.ReadAllText(csproj);
                    return content.Contains("<IsPackable>true</IsPackable>", StringComparison.OrdinalIgnoreCase)
                        && content.Contains("<DevelopmentDependency>true</DevelopmentDependency>", StringComparison.OrdinalIgnoreCase);
                })
                .OrderBy(path => path, StringComparer.Ordinal),
        ];
    }

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(AnalyzerPackageDependencyTests).Assembly.Location);
        while (dir is not null)
        {
            string gitPath = Path.Join(dir, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Could not find repository root (.git directory)");
    }
}
