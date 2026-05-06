using System.Reflection;
using Shouldly;
using Xunit;

namespace Granit.Taxonomy.Endpoints.Tests.Localization;

/// <summary>
/// Asserts the 18-culture coverage required by the Granit framework: 15 base cultures
/// (en, fr, nl, de, es, it, pt, zh, ja, pl, tr, ko, sv, cs, hi) + 3 regional variants
/// (en-GB, fr-CA, pt-BR), and that every base culture defines the three permission keys.
/// </summary>
public sealed class LocalizationFileTests
{
    private static readonly string[] BaseCultures =
        ["en", "fr", "nl", "de", "es", "it", "pt", "zh", "ja", "pl", "tr", "ko", "sv", "cs", "hi"];

    private static readonly string[] RegionalCultures =
        ["en-GB", "fr-CA", "pt-BR"];

    [Theory]
    [MemberData(nameof(AllCultures))]
    public void EmbeddedJsonFile_ExistsForCulture(string culture)
    {
        Assembly assembly = typeof(Granit.Taxonomy.Endpoints.Permissions.TaxonomyPermissions).Assembly;
        string fullResourceName =
            $"{assembly.GetName().Name}.Localization.TaxonomyEndpoints.{culture}.json";

        assembly.GetManifestResourceNames().ShouldContain(fullResourceName);
    }

    [Theory]
    [MemberData(nameof(BaseCultureRows))]
    public void BaseCulture_DefinesAllRequiredKeys(string culture)
    {
        Assembly assembly = typeof(Granit.Taxonomy.Endpoints.Permissions.TaxonomyPermissions).Assembly;
        string fullResourceName =
            $"{assembly.GetName().Name}.Localization.TaxonomyEndpoints.{culture}.json";

        using Stream? stream = assembly.GetManifestResourceStream(fullResourceName);
        stream.ShouldNotBeNull();
        using StreamReader reader = new(stream);
        string content = reader.ReadToEnd();

        content.ShouldContain("\"PermissionGroup:Taxonomy\"");
        content.ShouldContain("\"Permission:Taxonomy.Tags.Read\"");
        content.ShouldContain("\"Permission:Taxonomy.Tags.Manage\"");
        content.ShouldContain("\"Permission:Taxonomy.Search.Read\"");
    }

    public static TheoryData<string> AllCultures()
    {
        TheoryData<string> data = [];
        foreach (string c in BaseCultures)
        {
            data.Add(c);
        }
        foreach (string c in RegionalCultures)
        {
            data.Add(c);
        }
        return data;
    }

    public static TheoryData<string> BaseCultureRows()
    {
        TheoryData<string> data = [];
        foreach (string c in BaseCultures)
        {
            data.Add(c);
        }
        return data;
    }
}
