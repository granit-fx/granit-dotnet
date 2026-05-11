using Granit.IO.Diagnostics;
using Granit.IO.Internal;
using Granit.IO.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace Granit.IO.Tests;

[Collection("GranitIoMeter")]
public sealed class DefaultTempFileFactoryTests : IDisposable
{
    private readonly string _root;
    private readonly TempFileOptions _options;
    private readonly TestMeterFactory _meterFactory = new();
    private readonly IoMetrics _metrics;
    private readonly DefaultTempFileFactory _factory;
    private readonly FakeCurrentTenant _tenant = new();

    public DefaultTempFileFactoryTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "granit-io-tests-" + Guid.NewGuid().ToString("N"));
        _options = new TempFileOptions
        {
            RootDirectory = _root,
            RunJanitor = false,
            MaxSizeBytes = 1024,
        };
        _metrics = new IoMetrics(_meterFactory);
        _factory = new DefaultTempFileFactory(
            OptionsFactory.Create(_options),
            _metrics,
            NullLogger<DefaultTempFileFactory>.Instance,
            _tenant);
    }

    public void Dispose()
    {
        _meterFactory.Dispose();
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // best-effort cleanup
        }
    }

    [Fact]
    public async Task CreateAsync_ProducesFileInTenantPartition_WhenTenantAvailable()
    {
        Guid tenantId = Guid.NewGuid();
        using IDisposable _ = _tenant.Change(tenantId);

        await using ITempFile file = await _factory.CreateAsync("har", "bin");

        file.Path.ShouldContain($"t-{tenantId:N}");
        file.Path.ShouldContain($"{Path.DirectorySeparatorChar}har{Path.DirectorySeparatorChar}");
        File.Exists(file.Path).ShouldBeTrue();
    }

    [Fact]
    public async Task CreateAsync_ProducesFileUnderHost_WhenTenantUnavailable()
    {
        await using ITempFile file = await _factory.CreateAsync("trace", "json");

        file.Path.ShouldContain($"{Path.DirectorySeparatorChar}host{Path.DirectorySeparatorChar}");
        File.Exists(file.Path).ShouldBeTrue();
    }

    [Theory]
    [InlineData("Foo Bar")]
    [InlineData("foo_bar")]
    [InlineData("")]
    [InlineData("UPPER")]
    [InlineData("way-too-long-category-name-that-exceeds-the-32-character-cap")]
    public async Task CreateAsync_RejectsInvalidCategory(string category) =>
        await Should.ThrowAsync<ArgumentException>(async () =>
            await _factory.CreateAsync(category, "bin"));

    [Theory]
    [InlineData("bin file")]
    [InlineData("bin.gz")]
    [InlineData("")]
    [InlineData("waytoolongextensionname")]
    public async Task CreateAsync_RejectsInvalidExtension(string ext) =>
        await Should.ThrowAsync<ArgumentException>(async () =>
            await _factory.CreateAsync("har", ext));

    [Fact]
    public async Task CreateAsync_StripsLeadingDotInExtension()
    {
        await using ITempFile file = await _factory.CreateAsync("har", ".bin");

        file.Path.ShouldEndWith(".bin");
    }

    [Fact]
    public async Task CreateAsync_FileExists_DuringScope()
    {
        await using ITempFile file = await _factory.CreateAsync("har", "bin");

        File.Exists(file.Path).ShouldBeTrue();
    }

    [Fact]
    public async Task CreateAsync_FileDeleted_AfterDispose()
    {
        string path;
        await using (ITempFile file = await _factory.CreateAsync("har", "bin"))
        {
            path = file.Path;
            File.Exists(path).ShouldBeTrue();
        }

        // DeleteOnClose removes the file when the stream is closed.
        // On Windows, virus scanners can race; the factory's residual delete handles that.
        File.Exists(path).ShouldBeFalse();
    }

    [Fact]
    public async Task CreateAsync_FileMode_Is0600_OnLinux()
    {
        Skip.IfNot(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
            "POSIX file modes only apply on Linux/macOS.");

        await using ITempFile file = await _factory.CreateAsync("har", "bin");

        UnixFileMode mode = File.GetUnixFileMode(file.Path);
        mode.ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    [Fact]
    public async Task CreateAsync_DirectoryMode_Is0700_OnLinux()
    {
        Skip.IfNot(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
            "POSIX file modes only apply on Linux/macOS.");

        await using ITempFile file = await _factory.CreateAsync("har", "bin");

        string dir = Path.GetDirectoryName(file.Path)!;
        UnixFileMode mode = File.GetUnixFileMode(dir);
        mode.ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    [Fact]
    public async Task WriteBeyondMax_ThrowsIOException()
    {
        await using ITempFile file = await _factory.CreateAsync("har", "bin");

        byte[] buffer = new byte[(int)_options.MaxSizeBytes + 1];
        await Should.ThrowAsync<IOException>(async () =>
            await file.Stream.WriteAsync(buffer));
    }

    [Fact]
    public async Task Stream_CanRead_CanWrite_CanSeek()
    {
        await using ITempFile file = await _factory.CreateAsync("har", "bin");

        file.Stream.CanRead.ShouldBeTrue();
        file.Stream.CanWrite.ShouldBeTrue();
        file.Stream.CanSeek.ShouldBeTrue();
    }

    [Fact]
    public async Task TenantPartition_Disabled_PlacesFileUnderHost()
    {
        TempFileOptions opts = new()
        {
            RootDirectory = _root,
            RunJanitor = false,
            TenantPartition = false,
        };
        DefaultTempFileFactory factory = new(
            OptionsFactory.Create(opts),
            _metrics,
            NullLogger<DefaultTempFileFactory>.Instance,
            new FakeCurrentTenant(Guid.NewGuid()));

        await using ITempFile file = await factory.CreateAsync("har", "bin");

        file.Path.ShouldContain($"{Path.DirectorySeparatorChar}host{Path.DirectorySeparatorChar}");
    }

    [Fact]
    public async Task Metrics_Counter_Incremented()
    {
        using MeterListenerHarness harness = new("Granit.IO");

        await using (ITempFile file = await _factory.CreateAsync("har", "bin"))
        {
            harness.LongCounts("granit.io.temp.created").ShouldBeGreaterThanOrEqualTo(1);
        }

        harness.LongCounts("granit.io.temp.deleted").ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Constructor_NullOptions_Throws() =>
        Should.Throw<ArgumentNullException>(() => new DefaultTempFileFactory(
            null!,
            _metrics,
            NullLogger<DefaultTempFileFactory>.Instance));

    [Fact]
    public void Constructor_NullMetrics_Throws() =>
        Should.Throw<ArgumentNullException>(() => new DefaultTempFileFactory(
            OptionsFactory.Create(_options),
            null!,
            NullLogger<DefaultTempFileFactory>.Instance));

    [Fact]
    public void Constructor_NullLogger_Throws() =>
        Should.Throw<ArgumentNullException>(() => new DefaultTempFileFactory(
            OptionsFactory.Create(_options),
            _metrics,
            null!));

    [Fact]
    public async Task CreateAsync_WithoutCurrentTenantService_UsesHostPartition()
    {
        // Constructor with default `currentTenant = null`.
        DefaultTempFileFactory factory = new(
            OptionsFactory.Create(_options),
            _metrics,
            NullLogger<DefaultTempFileFactory>.Instance);

        await using ITempFile file = await factory.CreateAsync("har", "bin");

        file.Path.ShouldContain($"{Path.DirectorySeparatorChar}host{Path.DirectorySeparatorChar}");
    }

    [Fact]
    public void RootDirectory_DefaultsTo_GranitSubfolder()
    {
        TempFileOptions opts = new() { RootDirectory = null, RunJanitor = false };
        DefaultTempFileFactory factory = new(
            OptionsFactory.Create(opts),
            _metrics,
            NullLogger<DefaultTempFileFactory>.Instance);

        factory.RootDirectory.ShouldBe(Path.Combine(Path.GetTempPath(), "granit"));
    }

    private static class Skip
    {
        public static void IfNot(bool condition, string reason)
        {
            if (!condition)
            {
                Assert.Skip(reason);
            }
        }
    }
}
