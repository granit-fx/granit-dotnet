using System.Text.RegularExpressions;
using Shouldly;
using Xunit;
using AngleSharpLib = global::AngleSharp;

namespace Granit.Html.AngleSharp.Tests;

public sealed class AngleSharpConfigurationTests
{
    // Reach the type through the global alias so the test namespace (which ends in
    // ".AngleSharp") doesn't shadow the library's `AngleSharp` root.
    [Fact]
    public void BuildForTrustedTemplates_returns_a_configuration()
    {
        AngleSharpLib.IConfiguration config =
            global::Granit.Html.AngleSharp.AngleSharpConfiguration.BuildForTrustedTemplates();
        config.ShouldNotBeNull();
    }

    [Fact]
    public void BuildForUntrustedContent_returns_a_configuration()
    {
        AngleSharpLib.IConfiguration config =
            global::Granit.Html.AngleSharp.AngleSharpConfiguration.BuildForUntrustedContent();
        config.ShouldNotBeNull();
    }

    /// <summary>
    /// The untrusted-content profile MUST NEVER opt into AngleSharp's default loader. Doing so
    /// would let untrusted HTML fetch arbitrary URLs (SSRF / data exfiltration).
    ///
    /// We anchor the invariant on the source code rather than runtime behaviour because the
    /// loader is registered via a fluent builder — there is no public surface to inspect once
    /// an AngleSharp configuration has been built. Embedding the source as a resource keeps
    /// the check stable across refactors.
    /// </summary>
    [Fact]
    public void Source_must_never_call_WithDefaultLoader()
    {
        System.Reflection.Assembly assembly = typeof(AngleSharpConfigurationTests).Assembly;
        const string resourceName = "Granit.Html.AngleSharp.Source.AngleSharpConfiguration.cs";

        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        stream.ShouldNotBeNull(
            $"Embedded resource '{resourceName}' is required for the SSRF-loader archi test. " +
            "Check the EmbeddedResource entry in Granit.Html.AngleSharp.Tests.csproj.");

        using StreamReader reader = new(stream);
        string source = reader.ReadToEnd();

        // Strip line + block comments so xml-doc mentions of the method name don't trip the check.
        string code = StripComments(source);

        bool callsWithDefaultLoader = WithDefaultLoaderCallRegex.IsMatch(code);

        callsWithDefaultLoader.ShouldBeFalse(
            "AngleSharpConfiguration must not invoke WithDefaultLoader — that " +
            "would let untrusted HTML resolve external resources (SSRF). If a loader is " +
            "ever required for trusted templates, document the use case in the PR and " +
            "update this invariant deliberately.");
    }

    private static readonly Regex WithDefaultLoaderCallRegex =
        new(@"\.\s*WithDefaultLoader\s*\(", RegexOptions.Compiled);

    private static readonly Regex LineCommentRegex =
        new(@"//.*?$", RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex BlockCommentRegex =
        new(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.Compiled);

    private static string StripComments(string source)
    {
        string withoutBlock = BlockCommentRegex.Replace(source, string.Empty);
        return LineCommentRegex.Replace(withoutBlock, string.Empty);
    }
}
