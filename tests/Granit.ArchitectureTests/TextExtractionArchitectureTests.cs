using System.Text.RegularExpressions;
using System.Xml.Linq;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Architecture rules for the <c>Granit.TextExtraction.*</c> module family (epic #2233).
/// Pins the LimitedStream input-size contract and the "pure utility, no host plumbing" boundary
/// across the base package and the extractor providers (Email, Office, Pdf, Text, Tika,
/// Ocr.AI, Ocr.Tesseract).
/// </summary>
public sealed partial class TextExtractionArchitectureTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string SrcRoot = Path.Join(RepoRoot, "src");

    /// <summary>
    /// Single source of truth for every <c>Granit.TextExtraction.*</c> provider package
    /// that ships at least one extractor and must therefore satisfy the framework's
    /// purity / DependsOn pins.
    /// </summary>
    private static readonly string[] ProviderPackageNames =
    [
        "Granit.TextExtraction.Email",
        "Granit.TextExtraction.Ocr.AI",
        "Granit.TextExtraction.Ocr.Tesseract",
        "Granit.TextExtraction.Office",
        "Granit.TextExtraction.Pdf",
        "Granit.TextExtraction.Pdf.Ocr",
        "Granit.TextExtraction.Text",
        "Granit.TextExtraction.Tika",
    ];

    public static TheoryData<string> ProviderPackages
    {
        get
        {
            TheoryData<string> data = [];
            foreach (string p in ProviderPackageNames)
            {
                data.Add(p);
            }
            return data;
        }
    }

    /// <summary>
    /// Provider packages plus the base contract — used for the purity checks (no AspNetCore,
    /// no EF Core) that apply to every package in the family without exception.
    /// </summary>
    public static TheoryData<string> AllPackages
    {
        get
        {
            TheoryData<string> data = ["Granit.TextExtraction"];
            foreach (string p in ProviderPackageNames)
            {
                data.Add(p);
            }
            return data;
        }
    }

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
                .Any(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && File.ReadAllText(p).Contains("LimitedStream", StringComparison.Ordinal))
;

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
    [MemberData(nameof(AllPackages))]
    public void TextExtraction_packages_must_not_reference_AspNetCore(string projectName) =>
        AssertNoPackageReferenceStartsWith(projectName, "Microsoft.AspNetCore.");

    /// <summary>
    /// Same reasoning as <see cref="TextExtraction_packages_must_not_reference_AspNetCore"/>:
    /// TextExtraction is stateless. No DbContext, no migrations, no persistence concerns.
    /// EF Core in the build graph here would invite mission creep — a pipeline package starts
    /// caching extracted text into a table, and suddenly the framework has a DB dependency.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllPackages))]
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
    /// Every provider package must declare a <c>[DependsOn(...)]</c> attribute on its
    /// <c>*Module</c> class that references <c>GranitTextExtractionModule</c>. Without
    /// this, calling the provider's <c>Add*Extractor</c> extension would silently no-op
    /// (no pipeline service registered).
    /// </summary>
    [Theory]
    [MemberData(nameof(ProviderPackages))]
    public void Extractor_provider_modules_must_DependOn_base(string projectName)
    {
        string moduleFile = Directory
            .EnumerateFiles(Path.Join(SrcRoot, projectName), "Granit*Module.cs", SearchOption.TopDirectoryOnly)
            .SingleOrDefault()
            ?? throw new InvalidOperationException(
                $"Expected exactly one Granit*Module.cs under {projectName}.");

        string source = File.ReadAllText(moduleFile);

        // Match `[DependsOn(...)]` blocks (possibly spanning multiple typeof args)
        // and assert at least one references GranitTextExtractionModule. The looser
        // form keeps the pin honest for modules that legitimately depend on more
        // than one upstream module.
        bool dependsOnBase = DependsOnBaseModuleRegex().IsMatch(source);

        dependsOnBase.ShouldBeTrue(
            $"{projectName} must declare [DependsOn(... typeof(GranitTextExtractionModule) ...)] " +
            "on its module so the pipeline/options/metrics are registered when only this " +
            "provider is wired in.");
    }

    /// <summary>
    /// <c>Granit.TextExtraction.Office</c> opens user-supplied OOXML packages with the
    /// OpenXml SDK. Those packages can declare <c>&lt;Relationship Target="http://..."&gt;</c>
    /// entries pointing at attacker-controlled URLs. The OpenXml reader does not
    /// dereference them on its own, but a code path that wired in
    /// <c>HttpClient</c> / <c>WebRequest</c> / <c>Socket</c> by mistake would turn the
    /// extractor into an SSRF gadget.
    /// </summary>
    /// <remarks>
    /// We anchor the invariant on a source scan: if a file in the Office package
    /// references one of the forbidden APIs, the test fails. Strips line and block
    /// comments first so xml-doc mentions don't trip the check — same pattern as the
    /// VULN-300 <c>WithDefaultLoader</c> archi test in <c>Granit.Html.AngleSharp.Tests</c>.
    /// </remarks>
    [Fact]
    public void Office_assembly_must_not_reference_HTTP_or_external_URI_APIs()
    {
        string officeDir = Path.Join(SrcRoot, "Granit.TextExtraction.Office");
        Directory.Exists(officeDir).ShouldBeTrue($"Expected {officeDir} to exist.");

        List<string> violations = [];

        foreach (string csFile in Directory
            .EnumerateFiles(officeDir, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            string code = StripCommentsAndStrings(File.ReadAllText(csFile));
            string relativePath = Path.GetRelativePath(SrcRoot, csFile);

            foreach (Regex forbidden in ForbiddenNetworkingApiRegexes)
            {
                Match match = forbidden.Match(code);
                if (match.Success)
                {
                    violations.Add($"{relativePath}: {match.Value}");
                }
            }
        }

        violations.ShouldBeEmpty(
            "Granit.TextExtraction.Office must not reference HTTP / external-URI APIs " +
            "(HttpClient, WebRequest, WebClient, System.Net.Sockets.*). " +
            "OpenXml packages can declare external relationship targets — any networking " +
            "API in this assembly is a latent SSRF path. " +
            "If a code path genuinely needs networking, raise the requirement first; this " +
            "invariant only flips intentionally. Violations: " + string.Join(" | ", violations));
    }

    private static readonly Regex[] ForbiddenNetworkingApiRegexes =
    [
        UsingSystemNetHttpRegex(),
        UsingSystemNetSocketsRegex(),
        UsingSystemNetRegex(),
        SystemNetHttpMemberRegex(),
        SystemNetSocketsMemberRegex(),
        SystemNetWebApiRegex(),
        HttpClientRegex(),
        WebRequestRegex(),
        WebClientRegex(),
    ];

    [GeneratedRegex(@"\[DependsOn\([^\]]*typeof\(GranitTextExtractionModule\)[^\]]*\)\]")]
    private static partial Regex DependsOnBaseModuleRegex();

    [GeneratedRegex(@"\busing\s+System\.Net\.Http\b")]
    private static partial Regex UsingSystemNetHttpRegex();

    [GeneratedRegex(@"\busing\s+System\.Net\.Sockets\b")]
    private static partial Regex UsingSystemNetSocketsRegex();

    [GeneratedRegex(@"\busing\s+System\.Net\s*;")]
    private static partial Regex UsingSystemNetRegex();

    [GeneratedRegex(@"\bSystem\.Net\.Http\.")]
    private static partial Regex SystemNetHttpMemberRegex();

    [GeneratedRegex(@"\bSystem\.Net\.Sockets\.")]
    private static partial Regex SystemNetSocketsMemberRegex();

    [GeneratedRegex(@"\bSystem\.Net\.(WebRequest|WebClient)\b")]
    private static partial Regex SystemNetWebApiRegex();

    [GeneratedRegex(@"\bHttpClient\b")]
    private static partial Regex HttpClientRegex();

    [GeneratedRegex(@"\bWebRequest\b")]
    private static partial Regex WebRequestRegex();

    [GeneratedRegex(@"\bWebClient\b")]
    private static partial Regex WebClientRegex();

    [GeneratedRegex(@"//.*?$", RegexOptions.Multiline)]
    private static partial Regex LineCommentRegex();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex BlockCommentRegex();

    [GeneratedRegex(@"@""(?:""""|[^""])*""")]
    private static partial Regex VerbatimStringRegex();

    [GeneratedRegex(@"""(?:\\.|[^""\\])*""")]
    private static partial Regex RegularStringRegex();

    private static string StripCommentsAndStrings(string source)
    {
        // Comments first so a `// HttpClient is forbidden` xml-doc note doesn't trip
        // the scan, then strings so `"HttpClient.cs"` literals don't either.
        string s = BlockCommentRegex().Replace(source, string.Empty);
        s = LineCommentRegex().Replace(s, string.Empty);
        s = VerbatimStringRegex().Replace(s, "\"\"");
        s = RegularStringRegex().Replace(s, "\"\"");
        return s;
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
