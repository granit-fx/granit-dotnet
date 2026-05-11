using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Granit.Documents.AssetMetadata.Extractors;
using TagLib;

namespace Granit.Documents.AssetMetadata.AudioVideo.Internal;

/// <summary>
/// Audio / video extractor built on the TagLibSharp NuGet (LGPL-2.1,
/// dynamic-link). Reads the container tags (ID3, Vorbis comments, MP4
/// boxes, ...) and the codec properties, projects the well-known fields
/// into the typed columns of <see cref="AssetMetadataResult"/>, and
/// preserves every property verbatim in
/// <see cref="AssetMetadataResult.RawMetadata"/> under the
/// <c>audio:</c> or <c>video:</c> prefix (picked from the source MIME).
/// </summary>
internal sealed class AudioVideoMetadataExtractor : IAssetMetadataExtractor
{
    private const string AudioPrefix = "audio/";
    private const string VideoPrefix = "video/";

    /// <inheritdoc />
    public string Name => "audiovideo";

    /// <inheritdoc />
    public bool CanHandle(string sourceContentType)
    {
        if (string.IsNullOrWhiteSpace(sourceContentType))
        {
            return false;
        }

        string normalised = sourceContentType.Trim();
        return normalised.StartsWith(AudioPrefix, StringComparison.OrdinalIgnoreCase)
            || normalised.StartsWith(VideoPrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public Task<AssetMetadataResult> ExtractAsync(
        Stream source, string sourceContentType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceContentType);

        Stream readable = source;
        MemoryStream? buffered = null;
        try
        {
            if (!readable.CanSeek)
            {
                buffered = new MemoryStream();
                readable.CopyTo(buffered);
                buffered.Position = 0;
                readable = buffered;
            }
            else
            {
                readable.Position = 0;
            }

            string normalised = sourceContentType.Trim();
            bool isVideo = normalised.StartsWith(VideoPrefix, StringComparison.OrdinalIgnoreCase);
            string rawPrefix = isVideo ? "video" : "audio";
            string fileName = "input" + ExtensionFor(normalised);

            var abstraction = new StreamFileAbstraction(fileName, readable);
            using var file = TagLib.File.Create(abstraction);

            var raw = new Dictionary<string, string?>(StringComparer.Ordinal);

            Tag tag = file.Tag;
            string? title = NullIfEmpty(tag.Title);
            string? artist = NullIfEmpty(tag.FirstPerformer);
            string? album = NullIfEmpty(tag.Album);
            string? genre = NullIfEmpty(tag.FirstGenre);
            uint trackRaw = tag.Track;
            int? trackNumber = trackRaw > 0 ? (int)trackRaw : null;
            uint yearRaw = tag.Year;
            DateTimeOffset? takenAt = yearRaw > 0
                ? new DateTimeOffset(new DateTime((int)yearRaw, 1, 1, 0, 0, 0, DateTimeKind.Utc))
                : null;

            AddRaw(raw, rawPrefix, "Title", title);
            AddRaw(raw, rawPrefix, "Artist", artist);
            AddRaw(raw, rawPrefix, "Album", album);
            AddRaw(raw, rawPrefix, "Genre", genre);
            AddRaw(raw, rawPrefix, "Track", trackNumber?.ToString(CultureInfo.InvariantCulture));
            AddRaw(raw, rawPrefix, "Year", yearRaw > 0 ? yearRaw.ToString(CultureInfo.InvariantCulture) : null);
            AddRaw(raw, rawPrefix, "Comment", NullIfEmpty(tag.Comment));
            AddRaw(raw, rawPrefix, "Composer", NullIfEmpty(tag.FirstComposer));
            AddRaw(raw, rawPrefix, "AlbumArtist", NullIfEmpty(tag.FirstAlbumArtist));
            AddRaw(raw, rawPrefix, "Copyright", NullIfEmpty(tag.Copyright));
            AddRaw(raw, rawPrefix, "Disc", tag.Disc > 0 ? tag.Disc.ToString(CultureInfo.InvariantCulture) : null);

            long? durationMs = null;
            int? bitrate = null;
            string? codecDescription = null;
            int? width = null;
            int? height = null;

            Properties? props = file.Properties;
            if (props is not null)
            {
                TimeSpan duration = props.Duration;
                if (duration > TimeSpan.Zero)
                {
                    durationMs = (long)Math.Round(duration.TotalMilliseconds);
                    AddRaw(raw, rawPrefix, "DurationMs", durationMs.Value.ToString(CultureInfo.InvariantCulture));
                }

                if (props.AudioBitrate > 0)
                {
                    bitrate = props.AudioBitrate;
                    AddRaw(raw, rawPrefix, "AudioBitrate", bitrate.Value.ToString(CultureInfo.InvariantCulture));
                }
                if (props.AudioSampleRate > 0)
                {
                    AddRaw(raw, rawPrefix, "AudioSampleRate", props.AudioSampleRate.ToString(CultureInfo.InvariantCulture));
                }
                if (props.AudioChannels > 0)
                {
                    AddRaw(raw, rawPrefix, "AudioChannels", props.AudioChannels.ToString(CultureInfo.InvariantCulture));
                }

                foreach (ICodec codec in props.Codecs ?? [])
                {
                    if (codec is null)
                    {
                        continue;
                    }

                    codecDescription ??= NullIfEmpty(codec.Description);

                    if (codec is IVideoCodec video)
                    {
                        if (width is null && video.VideoWidth > 0)
                        {
                            width = video.VideoWidth;
                            AddRaw(raw, rawPrefix, "VideoWidth", width.Value.ToString(CultureInfo.InvariantCulture));
                        }
                        if (height is null && video.VideoHeight > 0)
                        {
                            height = video.VideoHeight;
                            AddRaw(raw, rawPrefix, "VideoHeight", height.Value.ToString(CultureInfo.InvariantCulture));
                        }
                    }
                }

                AddRaw(raw, rawPrefix, "Codec", codecDescription);
            }

            var result = new AssetMetadataResult("audiovideo", new Dictionary<string, string?>(StringComparer.Ordinal))
            {
                DurationMs = durationMs,
                Codec = codecDescription,
                Bitrate = bitrate,
                Width = width,
                Height = height,
                Artist = artist,
                Album = album,
                TrackNumber = trackNumber,
                Genre = genre,
                Title = title,
                TakenAt = takenAt,
            };

            return Task.FromResult(result with { RawMetadata = raw });
        }
        finally
        {
            buffered?.Dispose();
        }
    }

    private static void AddRaw(Dictionary<string, string?> raw, string prefix, string key, string? value)
    {
        if (value is not null)
        {
            raw[$"{prefix}:{key}"] = value;
        }
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string ExtensionFor(string mime) => mime.ToLowerInvariant() switch
    {
        "audio/mpeg" => ".mp3",
        "audio/mp3" => ".mp3",
        "audio/flac" or "audio/x-flac" => ".flac",
        "audio/ogg" or "audio/vorbis" => ".ogg",
        "audio/opus" => ".opus",
        "audio/wav" or "audio/x-wav" or "audio/wave" => ".wav",
        "audio/aac" => ".aac",
        "audio/mp4" or "audio/x-m4a" or "audio/m4a" => ".m4a",
        "video/mp4" => ".mp4",
        "video/webm" => ".webm",
        "video/quicktime" => ".mov",
        "video/x-msvideo" => ".avi",
        "video/x-matroska" => ".mkv",
        "video/mpeg" => ".mpeg",
        _ => ".bin",
    };
}
