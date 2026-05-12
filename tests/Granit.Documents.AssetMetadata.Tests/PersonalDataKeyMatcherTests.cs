using System.Collections.Generic;
using Granit.Documents.AssetMetadata.Internal;
using Shouldly;
using Xunit;

namespace Granit.Documents.AssetMetadata.Tests;

public sealed class PersonalDataKeyMatcherTests
{
    [Theory]
    [InlineData("exif:Author", true)]
    [InlineData("EXIF:AUTHOR", true)]
    [InlineData("xmp:rights.Owner", true)]
    [InlineData("iptc:Byline", true)]
    [InlineData("exif:CameraOwnerName", true)]
    [InlineData("exif:BodySerialNumber", true)]
    [InlineData("pdf:Creator", true)]
    [InlineData("office:LastModifiedBy", true)]
    [InlineData("audio:Copyright", true)]
    [InlineData("audio:Artist", true)]
    [InlineData("xmp:CreditLine", true)]
    [InlineData("xmp:Contact", true)]
    [InlineData("exif:Make", false)]
    [InlineData("exif:ImageWidth", false)]
    [InlineData("pdf:Title", false)]
    [InlineData("", false)]
    public void IsPersonalData_matches_substring_case_insensitively(string key, bool expected) =>
        PersonalDataKeyMatcher.IsPersonalData(key).ShouldBe(expected);

    [Fact]
    public void FindPersonalDataKeys_returns_only_matching_keys()
    {
        Dictionary<string, string?> raw = new()
        {
            ["exif:Author"] = "Alice",
            ["exif:Make"] = "Canon",
            ["xmp:CreditLine"] = "Acme Press",
            ["pdf:Title"] = "Report",
        };

        List<string> hits = PersonalDataKeyMatcher.FindPersonalDataKeys(raw);

        hits.Count.ShouldBe(2);
        hits.ShouldContain("exif:Author");
        hits.ShouldContain("xmp:CreditLine");
    }
}
