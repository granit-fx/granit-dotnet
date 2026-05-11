using System.Text.RegularExpressions;
using System.Xml.Linq;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Architecture rules pinning the <c>Granit.Documents.PublicLinks</c> module
/// boundaries (epic #1721, F18). Mirrors <see cref="AssetMetadataArchitectureTests"/>
/// and <see cref="RenditionsArchitectureTests"/>: the base contract package owns the
/// domain + <c>IDocumentPublicLinkService</c> interface; EFC plugs persistence on
/// top; the endpoints package wires the HTTP surface; no layer is allowed to leak
/// the raw bearer token or its HMAC digest onto a public surface.
/// </summary>
/// <remarks>
/// <para>
/// Boundary checks (base / EFC / endpoints purity) are enforced by parsing each
/// package's <c>.csproj</c> — direct project references and well-known
/// <c>PackageReference</c> ids (<c>Microsoft.AspNetCore.*</c>,
/// <c>Microsoft.EntityFrameworkCore</c>, <c>WolverineFx*</c>). This is consistent
/// with the source-grep approach used by sibling packages and avoids dragging
/// extra runtime dependencies into the architecture-tests project.
/// </para>
/// <para>
/// Token-leakage checks are enforced by source-grep over the public-links source
/// tree — looking for <c>public</c> properties / parameters named <c>Token</c> /
/// <c>TokenHash</c> outside an allowlist of audited types
/// (<see cref="AllowedTokenBearingTypes"/>) and for any <c>[LoggerMessage]</c>
/// template that mentions either token field.
/// </para>
/// </remarks>
public sealed partial class DocumentsPublicLinksArchitectureTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private const string BasePackage = "Granit.Documents.PublicLinks";
    private const string EfcPackage = "Granit.Documents.PublicLinks.EntityFrameworkCore";
    private const string EndpointsPackage = "Granit.Documents.PublicLinks.Endpoints";

    private static readonly string[] AllPackages =
    [
        BasePackage,
        EfcPackage,
        EndpointsPackage,
    ];

    /// <summary>
    /// Allowed declaration sites for a public <c>Token</c> / <c>TokenHash</c> member.
    /// Every other public surface in the public-links tree must keep the bearer
    /// material internal — credentials never travel on integration events / list
    /// responses / persistence DTOs.
    /// </summary>
    private static readonly string[] AllowedTokenBearingTypes =
    [
        // SingleValueObject wrapper around the raw token — exposes Value/string, not "Token".
        "PublicLinkToken",
        // Aggregate root: TokenHash is a persisted property on the domain entity
        // (kept public for EF Core materialisation). The raw token is never stored.
        "DocumentPublicLink",
        // One-time creation result: the only legitimate exit point for the raw token.
        "DocumentPublicLinkCreationResult",
        // One-time HTTP creation response — the bearer is returned exactly once.
        "CreatePublicLinkResponse",
    ];

    /// <summary>
    /// The base <c>Granit.Documents.PublicLinks</c> package must not reference
    /// EF Core, ASP.NET Core, or Wolverine — those concerns live in companion
    /// packages.
    /// </summary>
    [Fact]
    public void BaseModule_should_not_reference_EntityFrameworkCore() =>
        AssertCsprojDoesNotReferenceAny(
            package: BasePackage,
            packageIdPrefixes: ["Microsoft.EntityFrameworkCore"],
            projectRefSuffixes: [".EntityFrameworkCore.csproj"],
            because: "Granit.Documents.PublicLinks (base contract) must stay persistence-agnostic.");

    /// <summary>
    /// The base package must not pull ASP.NET Core in — HTTP wiring lives in
    /// <c>Granit.Documents.PublicLinks.Endpoints</c>.
    /// </summary>
    [Fact]
    public void BaseModule_should_not_reference_AspNetCore() =>
        AssertCsprojDoesNotReferenceAny(
            package: BasePackage,
            packageIdPrefixes: ["Microsoft.AspNetCore."],
            projectRefSuffixes: [".Endpoints.csproj"],
            frameworkReferences: ["Microsoft.AspNetCore.App"],
            because: "Granit.Documents.PublicLinks (base contract) must stay HTTP-agnostic.");

    /// <summary>
    /// The base package must not pull Wolverine in — distributed-event dispatch
    /// is wired by the host through <c>Granit.Events.Wolverine</c>; the base
    /// package only declares <c>*Eto</c> records via <c>Granit.Events</c>.
    /// </summary>
    [Fact]
    public void BaseModule_should_not_reference_Wolverine() =>
        AssertCsprojDoesNotReferenceAny(
            package: BasePackage,
            packageIdPrefixes: ["WolverineFx"],
            projectRefSuffixes: ["Granit.Wolverine.csproj", ".Wolverine.csproj"],
            because: "Granit.Documents.PublicLinks (base contract) must stay messaging-broker-agnostic.");

    /// <summary>
    /// The EFC companion must not pull ASP.NET Core in — persistence sits below
    /// the HTTP layer.
    /// </summary>
    [Fact]
    public void EntityFrameworkCoreModule_should_not_reference_AspNetCore() =>
        AssertCsprojDoesNotReferenceAny(
            package: EfcPackage,
            packageIdPrefixes: ["Microsoft.AspNetCore."],
            projectRefSuffixes: [".Endpoints.csproj"],
            frameworkReferences: ["Microsoft.AspNetCore.App"],
            because: "Granit.Documents.PublicLinks.EntityFrameworkCore must stay HTTP-agnostic.");

    /// <summary>
    /// Each companion package (EFC, Endpoints) must reference the base contract —
    /// otherwise it would be implementing a parallel surface.
    /// </summary>
    [Theory]
    [InlineData(EfcPackage)]
    [InlineData(EndpointsPackage)]
    public void Companion_package_should_reference_base_contract(string companion)
    {
        string csprojPath = Path.Join(RepoRoot, "src", companion, $"{companion}.csproj");
        File.Exists(csprojPath).ShouldBeTrue($"Expected {csprojPath} to exist.");

        var doc = XDocument.Load(csprojPath);
        bool referencesBase = doc.Descendants("ProjectReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
            .Any(r => r.EndsWith($"{BasePackage}.csproj", StringComparison.Ordinal) &&
                      !r.EndsWith($"{EfcPackage}.csproj", StringComparison.Ordinal) &&
                      !r.EndsWith($"{EndpointsPackage}.csproj", StringComparison.Ordinal));

        referencesBase.ShouldBeTrue(
            $"{companion} must reference the base Granit.Documents.PublicLinks contract.");
    }

    /// <summary>
    /// Service and store implementations must remain <c>internal sealed</c> —
    /// consumers compose them through the public <c>IDocumentPublicLinkService</c>
    /// interface, never by typing against the concrete class. The store interface
    /// itself stays <c>internal</c> on purpose: it's an EFC implementation detail.
    /// </summary>
    [Theory]
    [InlineData(EfcPackage, "DocumentPublicLinkService.cs", "internal sealed class DocumentPublicLinkService")]
    [InlineData(EfcPackage, "EfDocumentPublicLinkStore.cs", "internal sealed class EfDocumentPublicLinkStore")]
    [InlineData(EfcPackage, "IDocumentPublicLinkStore.cs", "internal interface IDocumentPublicLinkStore")]
    public void Service_implementations_should_be_internal_sealed(
        string package, string fileName, string expectedDeclaration)
    {
        string srcDir = Path.Join(RepoRoot, "src", package);
        string? match = Directory
            .EnumerateFiles(srcDir, fileName, SearchOption.AllDirectories)
            .Where(p => !IsBuildOutput(p))
            .FirstOrDefault();

        match.ShouldNotBeNull($"Expected to find {fileName} under {srcDir}.");
        string content = File.ReadAllText(match);
        content.ShouldContain(
            expectedDeclaration,
            customMessage: $"{fileName} must declare '{expectedDeclaration}' — concrete types stay internal sealed.");
    }

    /// <summary>
    /// No public surface in the public-links tree may expose a <c>Token</c> or
    /// <c>TokenHash</c> member outside the audited allowlist
    /// (<see cref="AllowedTokenBearingTypes"/>). Enforced by source-grep over the
    /// declared <c>public</c> records / classes — covers DTOs, ETOs, domain events,
    /// and option types alike without dragging a reflection load into the project.
    /// </summary>
    [Fact]
    public void Public_surface_should_not_leak_Token_or_TokenHash_members()
    {
        List<string> violators = [];

        foreach (string package in AllPackages)
        {
            string srcDir = Path.Join(RepoRoot, "src", package);
            if (!Directory.Exists(srcDir))
            {
                continue;
            }
            foreach (string csFile in Directory.EnumerateFiles(srcDir, "*.cs", SearchOption.AllDirectories))
            {
                if (IsBuildOutput(csFile))
                {
                    continue;
                }
                string content = File.ReadAllText(csFile);

                // 1. Public records — `public sealed record Foo(... Token ...)` /
                //    `public sealed record Foo(... TokenHash ...)`.
                foreach (Match m in PublicRecordDeclarationRegex().Matches(content))
                {
                    string typeName = m.Groups["name"].Value;
                    string parameters = m.Groups["params"].Value;
                    if (AllowedTokenBearingTypes.Contains(typeName, StringComparer.Ordinal))
                    {
                        continue;
                    }
                    if (ContainsTokenMemberRegex().IsMatch(parameters))
                    {
                        violators.Add($"{Path.GetRelativePath(RepoRoot, csFile)}: public record {typeName} exposes a Token/TokenHash member.");
                    }
                }

                // 2. Public class/record properties — `public byte[] TokenHash { get; ... }`
                //    or `public string Token { get; ... }` on a public type.
                foreach (Match m in PublicPropertyDeclarationRegex().Matches(content))
                {
                    string memberName = m.Groups["name"].Value;
                    string declaringType = NearestEnclosingTypeName(content, m.Index);
                    if (AllowedTokenBearingTypes.Contains(declaringType, StringComparer.Ordinal))
                    {
                        continue;
                    }
                    violators.Add(
                        $"{Path.GetRelativePath(RepoRoot, csFile)}: type '{declaringType}' exposes public property '{memberName}'.");
                }
            }
        }

        violators.ShouldBeEmpty(
            "Public surface in Granit.Documents.PublicLinks* must not expose Token/TokenHash members " +
            $"outside the audited allowlist ({string.Join(", ", AllowedTokenBearingTypes)}). " +
            $"Violators:\n{string.Join("\n", violators)}");
    }

    /// <summary>
    /// Defence-in-depth against accidental credential leakage in logs:
    /// no <c>[LoggerMessage]</c> attribute across the public-links tree may carry
    /// a message template referencing <c>{Token}</c> or <c>{TokenHash}</c>.
    /// </summary>
    [Fact]
    public void LoggerMessage_templates_should_not_reference_Token_or_TokenHash()
    {
        List<string> violators = [];

        foreach (string package in AllPackages)
        {
            string srcDir = Path.Join(RepoRoot, "src", package);
            if (!Directory.Exists(srcDir))
            {
                continue;
            }
            foreach (string csFile in Directory.EnumerateFiles(srcDir, "*.cs", SearchOption.AllDirectories))
            {
                if (IsBuildOutput(csFile))
                {
                    continue;
                }
                string content = File.ReadAllText(csFile);
                foreach (Match attr in LoggerMessageAttributeRegex().Matches(content))
                {
                    string args = attr.Groups["args"].Value;
                    if (LoggerMessageTokenPlaceholderRegex().IsMatch(args))
                    {
                        violators.Add(Path.GetRelativePath(RepoRoot, csFile));
                    }
                }
            }
        }

        violators.ShouldBeEmpty(
            "No [LoggerMessage] template may reference {Token} or {TokenHash}. " +
            $"Violators: {string.Join(", ", violators)}");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static void AssertCsprojDoesNotReferenceAny(
        string package,
        string[] packageIdPrefixes,
        string[] projectRefSuffixes,
        string because,
        string[]? frameworkReferences = null)
    {
        string csprojPath = Path.Join(RepoRoot, "src", package, $"{package}.csproj");
        File.Exists(csprojPath).ShouldBeTrue($"Expected {csprojPath} to exist.");

        var doc = XDocument.Load(csprojPath);

        IEnumerable<string> packageRefs = doc.Descendants("PackageReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty);
        string[] offendingPackages = packageRefs
            .Where(id => packageIdPrefixes.Any(p =>
                id.StartsWith(p, StringComparison.Ordinal) ||
                string.Equals(id, p.TrimEnd('.'), StringComparison.Ordinal)))
            .ToArray();

        IEnumerable<string> projectRefs = doc.Descendants("ProjectReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty);
        string[] offendingProjects = projectRefs
            .Where(r => projectRefSuffixes.Any(s => r.EndsWith(s, StringComparison.Ordinal)))
            .ToArray();

        string[] offendingFrameworkRefs = [];
        if (frameworkReferences is { Length: > 0 })
        {
            IEnumerable<string> fwRefs = doc.Descendants("FrameworkReference")
                .Select(e => e.Attribute("Include")?.Value ?? string.Empty);
            offendingFrameworkRefs = fwRefs
                .Where(f => frameworkReferences.Contains(f, StringComparer.Ordinal))
                .ToArray();
        }

        string[] allOffenders =
        [
            .. offendingPackages,
            .. offendingProjects,
            .. offendingFrameworkRefs,
        ];

        allOffenders.ShouldBeEmpty(
            $"{because} Offending references in {package}.csproj: {string.Join(", ", allOffenders)}");
    }

    private static string NearestEnclosingTypeName(string content, int index)
    {
        // Walk backwards through the file looking for the last `public ... class/record/struct Name`
        // declaration before <paramref name="index"/>. Good enough for the
        // single-type-per-file convention the codebase follows.
        string prefix = content[..index];
        Match? last = null;
        foreach (Match m in TypeDeclarationRegex().Matches(prefix))
        {
            last = m;
        }
        return last is null ? "<unknown>" : last.Groups["name"].Value;
    }

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    // Matches `public sealed record Foo(...)` or `public record Foo(...)` — captures the
    // primary-constructor parameter list so we can spot Token/TokenHash positional members.
    [GeneratedRegex(@"public\s+(?:sealed\s+)?record\s+(?<name>\w+)\s*\(\s*(?<params>[^)]*)\s*\)", RegexOptions.Singleline)]
    private static partial Regex PublicRecordDeclarationRegex();

    // Matches `Token` or `TokenHash` as a positional record parameter name —
    // anchored against a word boundary to avoid catching `ApiToken` and friends.
    [GeneratedRegex(@"\b(?:Token|TokenHash)\b\s*(?:,|$)", RegexOptions.Singleline)]
    private static partial Regex ContainsTokenMemberRegex();

    // Matches `public {type} Token { get; ... }` / `public {type} TokenHash { get; ... }`.
    [GeneratedRegex(@"public\s+[\w\[\]<>?,\s]+?\s+(?<name>Token|TokenHash)\s*\{\s*get\b", RegexOptions.Singleline)]
    private static partial Regex PublicPropertyDeclarationRegex();

    // Matches `public ... class|record|struct Name` (with optional sealed/partial).
    [GeneratedRegex(@"public\s+(?:sealed\s+|abstract\s+|partial\s+|static\s+)*(?:class|record|struct)\s+(?<name>\w+)", RegexOptions.Singleline)]
    private static partial Regex TypeDeclarationRegex();

    // Matches `[LoggerMessage(...)]` attribute applications, capturing the argument list.
    [GeneratedRegex(@"\[LoggerMessage\s*\((?<args>[^\]]*)\)\s*\]", RegexOptions.Singleline)]
    private static partial Regex LoggerMessageAttributeRegex();

    // Matches a `{Token}` or `{TokenHash}` placeholder anywhere in the LoggerMessage args.
    [GeneratedRegex(@"\{(?:Token|TokenHash)\}", RegexOptions.Singleline)]
    private static partial Regex LoggerMessageTokenPlaceholderRegex();

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(DocumentsPublicLinksArchitectureTests).Assembly.Location);
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
