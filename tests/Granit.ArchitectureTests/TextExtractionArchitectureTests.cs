using System.Xml.Linq;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Architecture rules for the <c>Granit.TextExtraction.*</c> module family (epic #2233).
/// Pins the VULN-001 input-size contract and the "pure utility, no host plumbing" boundary
/// across the base package and the extractor providers (PDF, Office, Text).
/// </summary>
public sealed class TextExtractionArchitectureTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string SrcRoot = Path.Join(RepoRoot, "src");

    /// <summary>
    /// Every <c>Granit.TextExtraction.*</c> package that ships at least one
    /// <c>*Extractor.cs</c> MUST mention <c>LimitedStream</c> somewhere in the package —
    /// proving that the input stream is wrapped before any third-party parser allocation.
    /// We check the package level (not the extractor file) because the wrap is often
    /// extracted to a shared helper (e.g. <c>OpenXmlExtraction</c>) that the per-format
    /// extractors delegate to.
    /// </summary>
    [Fact]
    public void Every_extractor_package_must_reference_LimitedStream()
    {
        List<string> violators = [];

        foreach (string packageDir in EnumerateExtractorPackages())
        {
            bool packageReferencesLimitedStream = Directory
                .EnumerateFiles(packageDir, "*.cs", SearchOption.AllDirectories)
                .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Any(p => File.ReadAllText(p).Contains("LimitedStream", StringComparison.Ordinal));

            if (!packageReferencesLimitedStream)
            {
                violators.Add(Path.GetFileName(packageDir));
            }
        }

        violators.ShouldBeEmpty(
            "Every Granit.TextExtraction.* package that ships extractors must wrap its " +
            "input in a LimitedStream — either directly in the extractor or via a shared " +
            "helper in the same package. Violators: " + string.Join(", ", violators));
    }

    /// <summary>
    /// <c>Granit.TextExtraction</c> is a pure utility framework: byte-stream → plain text.
    /// It must NEVER pull in ASP.NET Core — that would force every consumer (CLI tools,
    /// background workers, indexing pipelines) to drag the web stack into their build.
    /// </summary>
    [Theory]
    [InlineData("Granit.TextExtraction")]
    [InlineData("Granit.TextExtraction.Email")]
    [InlineData("Granit.TextExtraction.Office")]
    [InlineData("Granit.TextExtraction.Pdf")]
    [InlineData("Granit.TextExtraction.Text")]
    public void TextExtraction_packages_must_not_reference_AspNetCore(string projectName) =>
        AssertNoPackageReferenceStartsWith(projectName, "Microsoft.AspNetCore.");

    /// <summary>
    /// Same reasoning as <see cref="TextExtraction_packages_must_not_reference_AspNetCore"/>:
    /// TextExtraction is stateless. No DbContext, no migrations, no persistence concerns.
    /// EF Core in the build graph here would invite mission creep — a pipeline package starts
    /// caching extracted text into a table, and suddenly the framework has a DB dependency.
    /// </summary>
    [Theory]
    [InlineData("Granit.TextExtraction")]
    [InlineData("Granit.TextExtraction.Email")]
    [InlineData("Granit.TextExtraction.Office")]
    [InlineData("Granit.TextExtraction.Pdf")]
    [InlineData("Granit.TextExtraction.Text")]
    public void TextExtraction_packages_must_not_reference_EntityFrameworkCore(string projectName) =>
        AssertNoPackageReferenceStartsWith(projectName, "Microsoft.EntityFrameworkCore");

    /// <summary>
    /// The base <c>Granit.TextExtraction</c> contract package must declare zero
    /// <c>[DependsOn]</c> modules — it's the implicit root that every provider attaches to.
    /// A regression here would silently couple every host that adds the base contract to
    /// whatever the new dependency drags in (e.g. AI, vault, multi-tenancy).
    /// </summary>
    [Fact]
    public void Granit_TextExtraction_base_must_have_zero_module_dependencies()
    {
        string modulePath = Path.Join(
            SrcRoot, "Granit.TextExtraction", "GranitTextExtractionModule.cs");
        File.Exists(modulePath).ShouldBeTrue($"Expected {modulePath} to exist.");

        string source = File.ReadAllText(modulePath);
        source.ShouldNotContain(
            "[DependsOn",
            customMessage:
                "Granit.TextExtraction is the contract root for the byte-to-text pipeline. " +
                "Adding [DependsOn] here would force every provider package to inherit that " +
                "dependency. If a real coupling is needed, move it to the provider package.");
    }

    /// <summary>
    /// Every provider package (<c>Granit.TextExtraction.Email</c>,
    /// <c>Granit.TextExtraction.Office</c>, <c>Granit.TextExtraction.Pdf</c>,
    /// <c>Granit.TextExtraction.Text</c>) must declare a <c>[DependsOn(...)]</c>
    /// attribute that references <c>GranitTextExtractionModule</c> — either as the
    /// sole entry or alongside other module deps. Without this, calling
    /// <c>AddTextExtractor&lt;T&gt;</c> would silently no-op (no pipeline service
    /// registered).
    /// </summary>
    [Theory]
    [InlineData("Granit.TextExtraction.Email", "GranitTextExtractionEmailModule.cs")]
    [InlineData("Granit.TextExtraction.Office", "GranitTextExtractionOfficeModule.cs")]
    [InlineData("Granit.TextExtraction.Pdf", "GranitTextExtractionPdfModule.cs")]
    [InlineData("Granit.TextExtraction.Text", "GranitTextExtractionTextModule.cs")]
    public void Extractor_provider_modules_must_DependOn_base(string projectName, string moduleFile)
    {
        string modulePath = Path.Join(SrcRoot, projectName, moduleFile);
        File.Exists(modulePath).ShouldBeTrue($"Expected {modulePath} to exist.");

        string source = File.ReadAllText(modulePath);

        // Match `[DependsOn(...)]` blocks (possibly spanning multiple typeof args)
        // and assert at least one references GranitTextExtractionModule. The looser
        // form keeps the pin honest for modules that legitimately depend on more
        // than one upstream module (e.g. Email also depending on Html.AngleSharp).
        bool dependsOnBase = System.Text.RegularExpressions.Regex.IsMatch(
            source,
            @"\[DependsOn\([^\]]*typeof\(GranitTextExtractionModule\)[^\]]*\)\]");

        dependsOnBase.ShouldBeTrue(
            $"{projectName} must declare [DependsOn(... typeof(GranitTextExtractionModule) ...)] " +
            "on its module so the pipeline/options/metrics are registered when only this " +
            "provider is wired in.");
    }

    private static IEnumerable<string> EnumerateExtractorPackages()
    {
        foreach (string dir in Directory.EnumerateDirectories(SrcRoot, "Granit.TextExtraction*"))
        {
            // A package counts as an "extractor package" if it ships at least one
            // *Extractor.cs (other than the ITextExtractor interface). The base package
            // qualifies via PlainTextExtractor.cs.
            bool hasExtractor = Directory
                .EnumerateFiles(dir, "*Extractor.cs", SearchOption.TopDirectoryOnly)
                .Any(p => !Path.GetFileName(p).StartsWith("ITextExtractor", StringComparison.Ordinal));

            if (hasExtractor)
            {
                yield return dir;
            }
        }
    }

    private static void AssertNoPackageReferenceStartsWith(string projectName, string forbiddenPrefix)
    {
        string csprojPath = Path.Join(SrcRoot, projectName, $"{projectName}.csproj");
        File.Exists(csprojPath).ShouldBeTrue($"Expected {csprojPath} to exist.");

        var doc = XDocument.Load(csprojPath);
        IEnumerable<string> packageRefs = doc.Descendants("PackageReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty);

        IEnumerable<string> violators = packageRefs
            .Where(p => p.StartsWith(forbiddenPrefix, StringComparison.Ordinal));

        violators.ShouldBeEmpty(
            $"{projectName} must not reference NuGets starting with '{forbiddenPrefix}'. " +
            "TextExtraction is a pure byte→text utility — host concerns (HTTP, persistence) " +
            "belong upstream of the extraction pipeline. Violators: " +
            string.Join(", ", violators));
    }

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(TextExtractionArchitectureTests).Assembly.Location);
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
