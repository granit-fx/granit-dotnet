using Granit.IO.Diagnostics;
using Granit.IO.Internal;
using Granit.IO.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace Granit.IO.Tests;

public sealed class TempFileJanitorExecuteAsyncTests : IDisposable
{
    private readonly string _root;
    private readonly TestMeterFactory _meterFactory = new();
    private readonly IOMetrics _metrics;

    public TempFileJanitorExecuteAsyncTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "granit-io-execasync-" + Guid.NewGuid().ToString("N"));
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
            // best-effort
        }
    }

    [Fact]
    public async Task ExecuteAsync_RunJanitorFalse_ExitsImmediately()
    {
        TempFileOptions options = new()
        {
            RootDirectory = _root,
            RunJanitor = false,
            JanitorInterval = TimeSpan.FromMilliseconds(1),
        };
        TempFileJanitor janitor = new(
            OptionsFactory.Create(options),
            _metrics,
            TimeProvider.System,
            NullLogger<TempFileJanitor>.Instance);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(2));
        await janitor.StartAsync(cts.Token);
        await janitor.StopAsync(CancellationToken.None);

        // Reached here without hanging — that's the assertion.
        cts.IsCancellationRequested.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_TicksAndPurges()
    {
        FakeTimeProvider time = new(DateTimeOffset.UtcNow);

        TempFileOptions options = new()
        {
            RootDirectory = _root,
            RunJanitor = true,
            JanitorInterval = TimeSpan.FromSeconds(30),
            MaxLifetime = TimeSpan.FromMinutes(10),
        };

        string filePath = Path.Combine(_root, "stale.bin");
        File.WriteAllBytes(filePath, [1, 2]);
        File.SetLastWriteTimeUtc(filePath, time.GetUtcNow().UtcDateTime - TimeSpan.FromHours(1));

        TempFileJanitor janitor = new(
            OptionsFactory.Create(options),
            _metrics,
            time,
            NullLogger<TempFileJanitor>.Instance);

        await janitor.StartAsync(CancellationToken.None);

        // The first tick runs before any delay — file should be gone.
        // Give the background scheduler a brief moment.
        await Task.Delay(50);

        // Advance fake time past the interval so the loop iterates a second time.
        time.Advance(TimeSpan.FromMinutes(1));
        await Task.Delay(50);

        await janitor.StopAsync(CancellationToken.None);

        File.Exists(filePath).ShouldBeFalse();
    }
}
