using Granit.IpGeolocation.MaxMind.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.IpGeolocation.MaxMind.Tests;

public sealed class MaxMindIpGeolocationOptionsValidatorTests
{
    private readonly MaxMindIpGeolocationOptionsValidator _sut = new();

    [Fact]
    public void SectionName_IsNamespaceAlignedHierarchicalPath() =>
        MaxMindIpGeolocationOptions.SectionName.ShouldBe("IpGeolocation:MaxMind");

    [Fact]
    public void Validate_EmptyDatabasePath_Fails()
    {
        ValidateOptionsResult result = _sut.Validate(null, new MaxMindIpGeolocationOptions { DatabasePath = "" });

        result.Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_MissingFile_Fails()
    {
        MaxMindIpGeolocationOptions options = new() { DatabasePath = "/no/such/file.mmdb" };

        ValidateOptionsResult result = _sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_EmptyProviderName_Fails()
    {
        using TempFile file = new();
        MaxMindIpGeolocationOptions options = new() { DatabasePath = file.Path, ProviderName = " " };

        ValidateOptionsResult result = _sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ExistingFileAndProviderName_Succeeds()
    {
        using TempFile file = new();
        MaxMindIpGeolocationOptions options = new() { DatabasePath = file.Path };

        ValidateOptionsResult result = _sut.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    private sealed class TempFile : IDisposable
    {
        public TempFile() => Path = System.IO.Path.GetTempFileName();

        public string Path { get; }

        public void Dispose() => File.Delete(Path);
    }
}
