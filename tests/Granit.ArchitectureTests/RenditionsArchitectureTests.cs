using System.Xml.Linq;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Architecture rules pinning the <c>Granit.Documents.Renditions</c>
/// abstraction-then-provider shape (epic #1781). Mirrors
/// <see cref="BrowsingArchitectureTests"/>: the base contract package owns the
/// pipeline + provider interface; concrete provider packages (Imaging / Pdf /
/// Office) plug in on top via their own DI extensions and may not be referenced
/// from the base.
/// </summary>
public sealed class RenditionsArchitectureTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static readonly string[] RenditionPackages =
    [
        "Granit.Documents.Renditions",
        "Granit.Documents.Renditions.BackgroundJobs",
        "Granit.Documents.Renditions.EntityFrameworkCore",
        "Granit.Documents.Renditions.Endpoints",
        "Granit.Documents.Renditions.Imaging",
        "Granit.Documents.Renditions.Pdf",
        "Granit.Documents.Renditions.Office",
    ];

    /// <summary>
    /// Only <c>Granit.Documents.Renditions.Imaging</c> may pull in the
    /// <c>Granit.Imaging</c> abstraction among the renditions packages — every
    /// other renditions consumer routes through <c>IRenditionPipeline</c>.
    /// </summary>
    [Fact]
    public void Only_Imaging_provider_should_reference_Granit_Imaging() =>
        AssertOnlyAllowedRenditionPackageReferencesProjectRef(
            referencedProjectName: "Granit.Imaging",
            allowedRenditionPackage: "Granit.Documents.Renditions.Imaging");

    /// <summary>
    /// Only <c>Granit.Documents.Renditions.Pdf</c> may pull in
    /// <c>Granit.Browsing</c> among the renditions packages.
    /// </summary>
    [Fact]
    public void Only_Pdf_provider_should_reference_Granit_Browsing() =>
        AssertOnlyAllowedRenditionPackageReferencesProjectRef(
            referencedProjectName: "Granit.Browsing",
            allowedRenditionPackage: "Granit.Documents.Renditions.Pdf");

    /// <summary>
    /// Only <c>Granit.Documents.Renditions.Office</c> may shell out to
    /// LibreOffice via a <c>"soffice"</c> argument in <c>Process.Start</c>.
    /// </summary>
    [Fact]
    public void Only_Office_provider_should_invoke_soffice_binary()
    {
        List<string> violators = [];
        foreach (string package in RenditionPackages)
        {
            if (package == "Granit.Documents.Renditions.Office")
            {
                continue;
            }
            string srcDir = Path.Join(RepoRoot, "src", package);
            if (!Directory.Exists(srcDir))
            {
                continue;
            }
            foreach (string csFile in Directory.EnumerateFiles(srcDir, "*.cs", SearchOption.AllDirectories))
            {
                if (csFile.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                    csFile.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }
                string content = File.ReadAllText(csFile);
                if (content.Contains("soffice", StringComparison.OrdinalIgnoreCase))
                {
                    violators.Add(Path.GetRelativePath(RepoRoot, csFile));
                }
            }
        }

        violators.ShouldBeEmpty(
            "Only Granit.Documents.Renditions.Office may shell out to 'soffice'. " +
            "Other renditions packages must route through IRenditionProvider. " +
            $"Violators: {string.Join(", ", violators)}");
    }

    /// <summary>
    /// The base <c>Granit.Documents.Renditions</c> package must not reference
    /// any of its provider packages — that would invert the dependency direction
    /// and force every consumer to drag Magick.NET / Chromium / LibreOffice into
    /// the build graph.
    /// </summary>
    [Fact]
    public void Renditions_base_should_not_reference_provider_packages()
    {
        string csprojPath = Path.Join(
            RepoRoot, "src", "Granit.Documents.Renditions", "Granit.Documents.Renditions.csproj");
        File.Exists(csprojPath).ShouldBeTrue($"Expected {csprojPath} to exist.");

        var doc = XDocument.Load(csprojPath);
        IEnumerable<string> projectRefs = doc.Descendants("ProjectReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty);

        string[] providerSuffixes =
        [
            "Granit.Documents.Renditions.Imaging.csproj",
            "Granit.Documents.Renditions.Pdf.csproj",
            "Granit.Documents.Renditions.Office.csproj",
            "Granit.Documents.Renditions.BackgroundJobs.csproj",
            "Granit.Documents.Renditions.EntityFrameworkCore.csproj",
            "Granit.Documents.Renditions.Endpoints.csproj",
        ];

        IEnumerable<string> offending = projectRefs
            .Where(r => providerSuffixes.Any(s => r.EndsWith(s, StringComparison.Ordinal)));

        offending.ShouldBeEmpty(
            "Granit.Documents.Renditions (base abstraction) must not reference any " +
            "provider / EFC / endpoints / background-jobs package. Move the consumer " +
            "to the appropriate sibling instead.");
    }

    /// <summary>
    /// Every <c>Granit.Documents.Renditions.*</c> companion package must reference
    /// the base contract — otherwise it would be implementing a parallel surface.
    /// </summary>
    [Theory]
    [InlineData("Granit.Documents.Renditions.BackgroundJobs")]
    [InlineData("Granit.Documents.Renditions.EntityFrameworkCore")]
    [InlineData("Granit.Documents.Renditions.Endpoints")]
    [InlineData("Granit.Documents.Renditions.Imaging")]
    [InlineData("Granit.Documents.Renditions.Pdf")]
    [InlineData("Granit.Documents.Renditions.Office")]
    public void Companion_package_should_reference_Granit_Documents_Renditions(string companion)
    {
        string csprojPath = Path.Join(RepoRoot, "src", companion, $"{companion}.csproj");
        File.Exists(csprojPath).ShouldBeTrue($"Expected {csprojPath} to exist.");

        var doc = XDocument.Load(csprojPath);
        bool referencesBase = doc.Descendants("ProjectReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
            .Any(r => r.EndsWith("Granit.Documents.Renditions.csproj", StringComparison.Ordinal) &&
                      !r.Contains(".BackgroundJobs", StringComparison.Ordinal) &&
                      !r.Contains(".EntityFrameworkCore", StringComparison.Ordinal) &&
                      !r.Contains(".Endpoints", StringComparison.Ordinal) &&
                      !r.Contains(".Imaging", StringComparison.Ordinal) &&
                      !r.Contains(".Pdf", StringComparison.Ordinal) &&
                      !r.Contains(".Office", StringComparison.Ordinal));

        referencesBase.ShouldBeTrue(
            $"{companion} must reference the base Granit.Documents.Renditions contract.");
    }

    /// <summary>
    /// Every concrete type whose name ends with <c>RenditionProvider</c> in the
    /// renditions packages must declare implementation of <c>IRenditionProvider</c>.
    /// Verified by source-grep so the test does not need to load every provider
    /// assembly (which would drag the imaging / browsing / office runtime deps
    /// into the architecture-tests project).
    /// </summary>
    [Fact]
    public void Types_named_RenditionProvider_should_implement_IRenditionProvider()
    {
        List<string> violators = [];

        foreach (string package in RenditionPackages)
        {
            string srcDir = Path.Join(RepoRoot, "src", package);
            if (!Directory.Exists(srcDir))
            {
                continue;
            }
            foreach (string csFile in Directory.EnumerateFiles(srcDir, "*RenditionProvider.cs", SearchOption.AllDirectories))
            {
                if (csFile.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                    csFile.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }
                string content = File.ReadAllText(csFile);
                bool declaresProvider = System.Text.RegularExpressions.Regex.IsMatch(
                    content,
                    @"class\s+\w*RenditionProvider\b[^{]*:[^{]*\bIRenditionProvider\b");
                if (!declaresProvider)
                {
                    violators.Add(Path.GetRelativePath(RepoRoot, csFile));
                }
            }
        }

        violators.ShouldBeEmpty(
            "Types named *RenditionProvider must implement IRenditionProvider. " +
            $"Violators: {string.Join(", ", violators)}");
    }

    private static void AssertOnlyAllowedRenditionPackageReferencesProjectRef(
        string referencedProjectName,
        string allowedRenditionPackage)
    {
        List<string> violators = [];

        foreach (string package in RenditionPackages)
        {
            if (package == allowedRenditionPackage)
            {
                continue;
            }
            string csproj = Path.Join(RepoRoot, "src", package, $"{package}.csproj");
            if (!File.Exists(csproj))
            {
                continue;
            }

            var doc = XDocument.Load(csproj);
            bool referencesIt = doc.Descendants("ProjectReference")
                .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
                .Any(r => r.EndsWith($"{referencedProjectName}.csproj", StringComparison.Ordinal));

            if (referencesIt)
            {
                violators.Add(package);
            }
        }

        violators.ShouldBeEmpty(
            $"Only {allowedRenditionPackage} may reference {referencedProjectName} among the " +
            $"renditions packages. Other consumers must route through the rendition pipeline. " +
            $"Violators: {string.Join(", ", violators)}");
    }

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(RenditionsArchitectureTests).Assembly.Location);
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
