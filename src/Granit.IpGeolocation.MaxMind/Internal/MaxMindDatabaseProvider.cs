using System.Net;
using Granit.IpGeolocation.MaxMind.Options;
using MaxMind.Db;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.IpGeolocation.MaxMind.Internal;

/// <summary>
/// Owns the live <see cref="DatabaseReader"/>. Opens the <c>.mmdb</c> file (by default fully in memory, so no
/// OS file handle is retained) and, when configured, hot-reloads it: on a file change it builds a fresh reader
/// and atomically swaps it in under a lock, disposing the previous one. This lets a scheduled GeoIP update
/// replace the file underneath a running process without a restart or an "in use" failure.
/// </summary>
internal sealed partial class MaxMindDatabaseProvider : IDisposable
{
    private readonly MaxMindIpGeolocationOptions _options;
    private readonly ILogger<MaxMindDatabaseProvider> _logger;
    private readonly Lock _gate = new();
    private DatabaseReader _reader;
    private bool _isCityDatabase;
    private FileSystemWatcher? _watcher;

    public MaxMindDatabaseProvider(
        IOptions<MaxMindIpGeolocationOptions> options,
        ILogger<MaxMindDatabaseProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
        (_reader, _isCityDatabase) = Open();

        if (_options.ReloadOnChange)
        {
            StartWatching();
        }
    }

    /// <summary>Looks up <paramref name="address"/>, returning <c>null</c> when it is absent from the database.</summary>
    public GeoLocation? Lookup(IPAddress address)
    {
        DatabaseReader reader;
        bool isCity;
        lock (_gate)
        {
            reader = _reader;
            isCity = _isCityDatabase;
        }

        return isCity ? LookupCity(reader, address) : LookupCountry(reader, address);
    }

    private static GeoLocation? LookupCity(DatabaseReader reader, IPAddress address)
    {
        if (!reader.TryCity(address, out CityResponse? response) || response is null)
        {
            return null;
        }

        return new GeoLocation
        {
            City = response.City?.Name,
            Region = response.MostSpecificSubdivision?.Name,
            Country = response.Country?.Name,
            CountryCode = response.Country?.IsoCode,
            Latitude = response.Location?.Latitude,
            Longitude = response.Location?.Longitude,
            // The City database supplies an accuracy radius (km) — the primary low-confidence signal (mobile
            // NAT / sparse data give a large radius). Anonymising-IP classification (VPN/proxy/hosting) needs a
            // separate MaxMind Anonymous-IP database and is left to the IpInfo privacy provider / a follow-up,
            // so those flags stay null here.
            AccuracyRadiusKm = response.Location?.AccuracyRadius,
        };
    }

    private static GeoLocation? LookupCountry(DatabaseReader reader, IPAddress address)
    {
        if (!reader.TryCountry(address, out CountryResponse? response) || response is null)
        {
            return null;
        }

        return new GeoLocation
        {
            Country = response.Country?.Name,
            CountryCode = response.Country?.IsoCode,
        };
    }

    private (DatabaseReader Reader, bool IsCity) Open()
    {
        FileAccessMode mode = _options.FileAccess == MaxMindFileAccess.MemoryMapped
            ? FileAccessMode.MemoryMapped
            : FileAccessMode.Memory;

        DatabaseReader reader = new(_options.DatabasePath, mode);
        bool isCity = reader.Metadata.DatabaseType.Contains("City", StringComparison.OrdinalIgnoreCase);
        return (reader, isCity);
    }

    private void StartWatching()
    {
        string fullPath = Path.GetFullPath(_options.DatabasePath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        _watcher = new FileSystemWatcher(directory, Path.GetFileName(fullPath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnDatabaseFileChanged;
        _watcher.Created += OnDatabaseFileChanged;
        _watcher.Renamed += OnDatabaseFileChanged;
    }

    private void OnDatabaseFileChanged(object sender, FileSystemEventArgs e) => Reload();

    private void Reload()
    {
        try
        {
            (DatabaseReader newReader, bool isCity) = Open();
            DatabaseReader previous;
            lock (_gate)
            {
                previous = _reader;
                _reader = newReader;
                _isCityDatabase = isCity;
            }

            previous.Dispose();
            LogDatabaseReloaded();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDatabaseException)
        {
            // The file is likely mid-write; a subsequent event will retry once the swap completes.
            LogDatabaseReloadDeferred(ex);
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _reader.Dispose();
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "MaxMind geolocation database reloaded.")]
    private partial void LogDatabaseReloaded();

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "MaxMind geolocation database change could not be applied yet; will retry on the next change.")]
    private partial void LogDatabaseReloadDeferred(Exception exception);
}
