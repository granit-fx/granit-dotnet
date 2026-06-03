using Granit.ArchitectureTests.Abstractions.Rules;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests.Abstractions.Tests;

/// <summary>
/// Behavioural tests for <see cref="LocalizationFileShapeRules"/>, focused on the
/// <c>allowEmpty</c> opt-out: strict-by-default keeps the broken-glob canary, while
/// minimalist consumers can skip the empty check without losing per-file validation.
/// </summary>
public sealed class LocalizationFileShapeRulesTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("granit-loc-rules-");

    public void Dispose()
    {
        try
        {
            _root.Delete(recursive: true);
        }
        catch (IOException)
        {
            // best-effort cleanup of the temp fixture
        }
    }

    [Fact]
    public void No_localization_files_with_default_strictness_fails()
    {
        Should.Throw<Shouldly.ShouldAssertException>(() =>
            LocalizationFileShapeRules.EveryLocalizationFileShouldUseCultureEnvelope(_root.FullName, _root.FullName));
    }

    [Fact]
    public void No_localization_files_with_allowEmpty_passes()
    {
        Should.NotThrow(() =>
            LocalizationFileShapeRules.EveryLocalizationFileShouldUseCultureEnvelope(
                _root.FullName, _root.FullName, allowEmpty: true));
    }

    [Fact]
    public void Valid_culture_envelope_passes()
    {
        WriteLocalizationFile("en.json", """{ "culture": "en", "texts": { "Greeting": "Hello" } }""");

        Should.NotThrow(() =>
            LocalizationFileShapeRules.EveryLocalizationFileShouldUseCultureEnvelope(_root.FullName, _root.FullName));
    }

    [Fact]
    public void Malformed_file_still_fails_even_with_allowEmpty()
    {
        // The opt-out must only suppress the "zero files" canary — never per-file validation.
        WriteLocalizationFile("en.json", """{ "Greeting": "Hello" }""");

        Should.Throw<Shouldly.ShouldAssertException>(() =>
            LocalizationFileShapeRules.EveryLocalizationFileShouldUseCultureEnvelope(
                _root.FullName, _root.FullName, allowEmpty: true));
    }

    [Fact]
    public void Culture_value_mismatching_filename_fails()
    {
        WriteLocalizationFile("fr.json", """{ "culture": "en", "texts": { } }""");

        Should.Throw<Shouldly.ShouldAssertException>(() =>
            LocalizationFileShapeRules.EveryLocalizationFileShouldUseCultureEnvelope(_root.FullName, _root.FullName));
    }

    private void WriteLocalizationFile(string fileName, string content)
    {
        string dir = Path.Join(_root.FullName, "Granit.Sample", "Localization", "Sample");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Join(dir, fileName), content);
    }
}
