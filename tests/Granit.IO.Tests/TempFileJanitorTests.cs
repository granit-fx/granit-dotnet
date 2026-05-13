using Granit.IO.Diagnostics;
using Granit.IO.Internal;
using Granit.IO.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace Granit.IO.Tests;

public sealed class TempFileJanitorTests : IDisposable
{
    private readonly string _root;
    private readonly TestMeterFactory _meterFactory = new();
    private readonly IOMetrics _metrics;

    public TempFileJanitorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "granit-io-janitor-" + Guid.NewGuid().ToString("N"));
        _metrics = new IOMetrics(_meterFactory);
        Directory.CreateDirectory(_root);
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

    private TempFileJanitor BuildJanitor(TimeProvider time, TimeSpan? maxLifetime = null)
    {
        TempFileOptions options = new()
        {
            RootDirectory = _root,
            MaxLifetime = maxLifetime ?? TimeSpan.FromMinutes(30),
            RunJanitor = true,
            JanitorInterval = TimeSpan.FromMinutes(1),
        };

        return new TempFileJanitor(
            OptionsFactory.Create(options),
            _metrics,
            time,
            NullLogger<TempFileJanitor>.Instance);
    }

    [Fact]
    public void RunOnce_OldFilePurged()
    {
        FakeTimeProvider time = new(DateTimeOffset.UtcNow);
        TempFileJanitor janitor = BuildJanitor(time, TimeSpan.FromMinutes(10));

        string filePath = Path.Combine(_root, "stale.bin");
        File.WriteAllBytes(filePath, [1, 2, 3]);
        File.SetLastWriteTimeUtc(filePath, time.GetUtcNow().UtcDateTime - TimeSpan.FromHours(1));

        janitor.RunOnce();

        File.Exists(filePath).ShouldBeFalse();
    }

    [Fact]
    public void RunOnce_RecentFileKept()
    {
        FakeTimeProvider time = new(DateTimeOffset.UtcNow);
        TempFileJanitor janitor = BuildJanitor(time, TimeSpan.FromMinutes(30));

        string filePath = Path.Combine(_root, "fresh.bin");
        File.WriteAllBytes(filePath, [1, 2, 3]);
        File.SetLastWriteTimeUtc(filePath, time.GetUtcNow().UtcDateTime);

        janitor.RunOnce();

        File.Exists(filePath).ShouldBeTrue();
    }

    [Fact]
    public void RunOnce_MixedFiles_PurgesOldOnly()
    {
        FakeTimeProvider time = new(DateTimeOffset.UtcNow);
        TempFileJanitor janitor = BuildJanitor(time, TimeSpan.FromMinutes(10));

        string oldFile = Path.Combine(_root, "old.bin");
        string newFile = Path.Combine(_root, "new.bin");
        File.WriteAllBytes(oldFile, [1]);
        File.WriteAllBytes(newFile, [2]);
        File.SetLastWriteTimeUtc(oldFile, time.GetUtcNow().UtcDateTime - TimeSpan.FromHours(1));
        File.SetLastWriteTimeUtc(newFile, time.GetUtcNow().UtcDateTime);

        janitor.RunOnce();

        File.Exists(oldFile).ShouldBeFalse();
        File.Exists(newFile).ShouldBeTrue();
    }

    [Fact]
    public void RunOnce_NonExistentRoot_NoOp()
    {
        FakeTimeProvider time = new(DateTimeOffset.UtcNow);
        TempFileOptions options = new()
        {
            RootDirectory = Path.Combine(_root, "does-not-exist"),
            MaxLifetime = TimeSpan.FromMinutes(10),
            RunJanitor = true,
        };
        TempFileJanitor janitor = new(
            OptionsFactory.Create(options),
            _metrics,
            time,
            NullLogger<TempFileJanitor>.Instance);

        Should.NotThrow(janitor.RunOnce);
    }

    [Fact]
    public void RunOnce_RecursesIntoSubdirectories()
    {
        FakeTimeProvider time = new(DateTimeOffset.UtcNow);
        TempFileJanitor janitor = BuildJanitor(time, TimeSpan.FromMinutes(10));

        string subDir = Path.Combine(_root, "t-abc", "har");
        Directory.CreateDirectory(subDir);
        string filePath = Path.Combine(subDir, "stale.bin");
        File.WriteAllBytes(filePath, [1, 2, 3]);
        File.SetLastWriteTimeUtc(filePath, time.GetUtcNow().UtcDateTime - TimeSpan.FromHours(1));

        janitor.RunOnce();

        File.Exists(filePath).ShouldBeFalse();
    }

    [Fact]
    public void RunOnce_SurvivesIOExceptionOnOneFile()
    {
        FakeTimeProvider time = new(DateTimeOffset.UtcNow);
        TempFileJanitor janitor = BuildJanitor(time, TimeSpan.FromMinutes(10));

        string stale = Path.Combine(_root, "stale1.bin");
        string locked = Path.Combine(_root, "locked.bin");
        string stale2 = Path.Combine(_root, "stale2.bin");

        File.WriteAllBytes(stale, [1]);
        File.WriteAllBytes(locked, [1]);
        File.WriteAllBytes(stale2, [1]);

        DateTime backdated = time.GetUtcNow().UtcDateTime - TimeSpan.FromHours(1);
        File.SetLastWriteTimeUtc(stale, backdated);
        File.SetLastWriteTimeUtc(locked, backdated);
        File.SetLastWriteTimeUtc(stale2, backdated);

        // Hold an exclusive handle on `locked` so the janitor's Delete throws IOException.
        using FileStream hold = new(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        janitor.RunOnce();

        File.Exists(stale).ShouldBeFalse();
        File.Exists(stale2).ShouldBeFalse();
        // The locked file is allowed to remain — we just want the loop to survive.
    }

    [Fact]
    public void RootDirectory_DefaultsTo_GranitSubfolder()
    {
        FakeTimeProvider time = new(DateTimeOffset.UtcNow);
        TempFileOptions options = new()
        {
            RootDirectory = null,
            RunJanitor = true,
        };
        TempFileJanitor janitor = new(
            OptionsFactory.Create(options),
            _metrics,
            time,
            NullLogger<TempFileJanitor>.Instance);

        janitor.RootDirectory.ShouldBe(Path.Combine(Path.GetTempPath(), "granit"));
    }
}
