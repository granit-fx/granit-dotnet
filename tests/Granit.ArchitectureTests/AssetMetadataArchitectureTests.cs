using System.Text.RegularExpressions;
using System.Xml.Linq;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Architecture rules pinning the <c>Granit.Documents.AssetMetadata</c>
/// abstraction-then-provider shape (epic #1721, F17). Mirrors
/// <see cref="RenditionsArchitectureTests"/>: the base contract package owns the
/// pipeline + <c>IAssetMetadataExtractor</c> contract; concrete extractor
/// packages (Imaging / Pdf / Office / AudioVideo) plug in on top via their own
/// DI extensions and pull in their format-specific NuGet (MetadataExtractor /
/// PdfPig / DocumentFormat.OpenXml / TagLibSharp) — and only theirs.
/// </summary>
public sealed partial class AssetMetadataArchitectureTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static readonly string[] AssetMetadataPackages =
    [
        "Granit.Documents.AssetMetadata",
        "Granit.Documents.AssetMetadata.BackgroundJobs",
        "Granit.Documents.AssetMetadata.EntityFrameworkCore",
        "Granit.Documents.AssetMetadata.Endpoints",
        "Granit.Documents.AssetMetadata.Imaging",
        "Granit.Documents.AssetMetadata.Pdf",
        "Granit.Documents.AssetMetadata.Office",
        "Granit.Documents.AssetMetadata.AudioVideo",
    ];

    /// <summary>
    /// Only <c>Granit.Documents.AssetMetadata.Imaging</c> may pull in the
    /// <c>MetadataExtractor</c> NuGet among the asset-metadata packages — every
    /// other consumer routes through <c>IAssetMetadataExtractor</c>.
    /// </summary>
    [Fact]
    public void Only_Imaging_extractor_should_reference_MetadataExtractor_nuget() =>
        AssertOnlyAllowedPackageReferencesNuGet(
            packageId: "MetadataExtractor",
            allowedAssetMetadataPackage: "Granit.Documents.AssetMetadata.Imaging");

    /// <summary>
    /// Only <c>Granit.Documents.AssetMetadata.Pdf</c> may pull in the
    /// <c>PdfPig</c> NuGet among the asset-metadata packages.
    /// </summary>
    [Fact]
    public void Only_Pdf_extractor_should_reference_PdfPig_nuget() =>
        AssertOnlyAllowedPackageReferencesNuGet(
            packageId: "PdfPig",
            allowedAssetMetadataPackage: "Granit.Documents.AssetMetadata.Pdf");

    /// <summary>
    /// Only <c>Granit.Documents.AssetMetadata.Office</c> may pull in the
    /// <c>DocumentFormat.OpenXml</c> NuGet among the asset-metadata packages.
    /// </summary>
    [Fact]
    public void Only_Office_extractor_should_reference_DocumentFormat_OpenXml_nuget() =>
        AssertOnlyAllowedPackageReferencesNuGet(
            packageId: "DocumentFormat.OpenXml",
            allowedAssetMetadataPackage: "Granit.Documents.AssetMetadata.Office");

    /// <summary>
    /// Only <c>Granit.Documents.AssetMetadata.AudioVideo</c> may pull in the
    /// <c>TagLibSharp</c> NuGet among the asset-metadata packages.
    /// </summary>
    [Fact]
    public void Only_AudioVideo_extractor_should_reference_TagLibSharp_nuget() =>
        AssertOnlyAllowedPackageReferencesNuGet(
            packageId: "TagLibSharp",
            allowedAssetMetadataPackage: "Granit.Documents.AssetMetadata.AudioVideo");

    /// <summary>
    /// The base <c>Granit.Documents.AssetMetadata</c> package must not reference
    /// any of its provider / companion packages — that would invert the
    /// dependency direction and force every consumer to drag MetadataExtractor /
    /// PdfPig / OpenXml / TagLibSharp into the build graph.
    /// </summary>
    [Fact]
    public void AssetMetadata_base_should_not_reference_provider_packages()
    {
        string csprojPath = Path.Join(
            RepoRoot, "src", "Granit.Documents.AssetMetadata", "Granit.Documents.AssetMetadata.csproj");
        File.Exists(csprojPath).ShouldBeTrue($"Expected {csprojPath} to exist.");

        var doc = XDocument.Load(csprojPath);
        IEnumerable<string> projectRefs = doc.Descendants("ProjectReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty);

        string[] providerSuffixes =
        [
            "Granit.Documents.AssetMetadata.Imaging.csproj",
            "Granit.Documents.AssetMetadata.Pdf.csproj",
            "Granit.Documents.AssetMetadata.Office.csproj",
            "Granit.Documents.AssetMetadata.AudioVideo.csproj",
            "Granit.Documents.AssetMetadata.BackgroundJobs.csproj",
            "Granit.Documents.AssetMetadata.EntityFrameworkCore.csproj",
            "Granit.Documents.AssetMetadata.Endpoints.csproj",
        ];

        IEnumerable<string> offending = projectRefs
            .Where(r => providerSuffixes.Any(s => r.EndsWith(s, StringComparison.Ordinal)));

        offending.ShouldBeEmpty(
            "Granit.Documents.AssetMetadata (base abstraction) must not reference any " +
            "provider / EFC / endpoints / background-jobs package. Move the consumer " +
            "to the appropriate sibling instead.");
    }

    /// <summary>
    /// Every concrete type whose name ends with <c>MetadataExtractor</c> in the
    /// asset-metadata provider packages must declare implementation of
    /// <c>IAssetMetadataExtractor</c>. Verified by source-grep so the test does
    /// not need to load every provider assembly (which would drag the
    /// MetadataExtractor / PdfPig / OpenXml / TagLibSharp runtime deps into the
    /// architecture-tests project).
    /// </summary>
    [Fact]
    public void Types_named_MetadataExtractor_should_implement_IAssetMetadataExtractor()
    {
        List<string> violators = [];

        foreach (string package in AssetMetadataPackages)
        {
            string srcDir = Path.Join(RepoRoot, "src", package);
            if (!Directory.Exists(srcDir))
            {
                continue;
            }
            foreach (string csFile in Directory.EnumerateFiles(srcDir, "*MetadataExtractor.cs", SearchOption.AllDirectories))
            {
                if (csFile.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                    csFile.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }
                // Skip the interface contract itself — only concrete *MetadataExtractor classes are gated.
                if (Path.GetFileName(csFile).StartsWith("IAssetMetadata", StringComparison.Ordinal))
                {
                    continue;
                }
                string content = File.ReadAllText(csFile);
                bool declaresExtractor = ExtractorDeclarationRegex().IsMatch(content);
                if (!declaresExtractor)
                {
                    violators.Add(Path.GetRelativePath(RepoRoot, csFile));
                }
            }
        }

        violators.ShouldBeEmpty(
            "Types named *MetadataExtractor must implement IAssetMetadataExtractor. " +
            $"Violators: {string.Join(", ", violators)}");
    }

    private static void AssertOnlyAllowedPackageReferencesNuGet(
        string packageId,
        string allowedAssetMetadataPackage)
    {
        List<string> violators = [];

        foreach (string package in AssetMetadataPackages)
        {
            if (package == allowedAssetMetadataPackage)
            {
                continue;
            }
            string csproj = Path.Join(RepoRoot, "src", package, $"{package}.csproj");
            if (!File.Exists(csproj))
            {
                continue;
            }

            var doc = XDocument.Load(csproj);
            bool referencesIt = doc.Descendants("PackageReference")
                .Any(e => string.Equals(
                    e.Attribute("Include")?.Value, packageId, StringComparison.Ordinal));

            if (referencesIt)
            {
                violators.Add(package);
            }
        }

        violators.ShouldBeEmpty(
            $"Only {allowedAssetMetadataPackage} may reference the {packageId} NuGet among the " +
            $"asset-metadata packages. Other consumers must route through IAssetMetadataExtractor. " +
            $"Violators: {string.Join(", ", violators)}");
    }

    [GeneratedRegex(@"class\s+\w*MetadataExtractor\b[^{]*:[^{]*\bIAssetMetadataExtractor\b")]
    private static partial Regex ExtractorDeclarationRegex();

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(AssetMetadataArchitectureTests).Assembly.Location);
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
