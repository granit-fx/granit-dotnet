using System;
using System.IO;
using TagLib;

namespace Granit.Documents.AssetMetadata.AudioVideo.Tests;

/// <summary>
/// Synthesises tiny audio + video fixtures at runtime so the repo never
/// carries committed binary blobs. The WAV is built from raw RIFF bytes
/// then tagged via TagLibSharp; the MP4 is a hand-rolled minimal ISO BMFF
/// container with a video track plus an iTunes-style <c>moov/udta/meta</c>
/// metadata block.
/// </summary>
internal static class AudioVideoFixtures
{
    /// <summary>
    /// Builds a tiny mono 8-bit PCM WAV (~8 KB for one second) and writes
    /// the supplied ID3-style tags back via TagLibSharp so the extractor
    /// has something to read on the round-trip.
    /// </summary>
    public static byte[] BuildWavWithTags(
        int sampleRate,
        int durationSeconds,
        string? title = null,
        string? artist = null,
        string? album = null,
        string? genre = null,
        int? track = null,
        int? year = null)
    {
        byte[] wav = BuildRawWav(sampleRate, durationSeconds);
        using MemoryStream ms = new();
        ms.Write(wav, 0, wav.Length);
        ms.Position = 0;

        var abstraction = new MutableStreamFileAbstraction("input.wav", ms);
        using (var file = TagLib.File.Create(abstraction))
        {
            if (title is not null)
            {
                file.Tag.Title = title;
            }
            if (artist is not null)
            {
                file.Tag.Performers = [artist];
            }
            if (album is not null)
            {
                file.Tag.Album = album;
            }
            if (genre is not null)
            {
                file.Tag.Genres = [genre];
            }
            if (track is not null)
            {
                file.Tag.Track = (uint)track.Value;
            }
            if (year is not null)
            {
                file.Tag.Year = (uint)year.Value;
            }
            file.Save();
        }

        return ms.ToArray();
    }

    private static byte[] BuildRawWav(int sampleRate, int durationSeconds)
    {
        const int channels = 1;
        const int bitsPerSample = 8;
        int byteRate = sampleRate * channels * bitsPerSample / 8;
        int dataLen = byteRate * durationSeconds;

        using MemoryStream ms = new();
        using BinaryWriter w = new(ms);
        w.Write("RIFF"u8);
        w.Write(36 + dataLen);
        w.Write("WAVE"u8);
        w.Write("fmt "u8);
        w.Write(16);                                  // PCM fmt chunk size
        w.Write((short)1);                            // PCM format
        w.Write((short)channels);
        w.Write(sampleRate);
        w.Write(byteRate);
        w.Write((short)(channels * bitsPerSample / 8));
        w.Write((short)bitsPerSample);
        w.Write("data"u8);
        w.Write(dataLen);
        // Silence == 0x80 for 8-bit unsigned PCM.
        for (int i = 0; i < dataLen; i++)
        {
            w.Write((byte)0x80);
        }
        return ms.ToArray();
    }

    /// <summary>
    /// Returns the bytes of a 320x240 / 2s minimal MP4 container with a
    /// <c>moov/udta/meta</c> iTunes block carrying title / artist / album
    /// / genre / year. The fixture is hand-crafted ISO BMFF, base64-inlined
    /// so the test project carries no committed binaries.
    /// </summary>
    public static byte[] MinimalMp4WithMetadata() => Convert.FromBase64String(Mp4FixtureBase64);

    /// <summary>
    /// Read/write <see cref="TagLib.File.IFileAbstraction"/> used by the
    /// fixture builder to mutate the WAV in place. The production
    /// extractor never needs a writable stream — its abstraction is
    /// read-only.
    /// </summary>
    private sealed class MutableStreamFileAbstraction(string name, Stream stream) : TagLib.File.IFileAbstraction
    {
        public string Name { get; } = name;
        public Stream ReadStream { get; } = stream;
        public Stream WriteStream { get; } = stream;
        public void CloseStream(Stream stream) { /* caller-owned. */ }
    }

    private const string Mp4FixtureBase64 =
        "AAAAIGZ0eXBpc29tAAACAGlzb21pc28yYXZjMW1wNDEAAAMBbW9vdgAAAGxtdmhkAAAAAAAAAAAAAAAAAAAD6AAA" +
        "B9AAAQAAAQAAAAAAAAAAAAAAAAEAAAAAAAAAAAAAAAAAAAABAAAAAAAAAAAAAAAAAABAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAgAAAat0cmFrAAAAXHRraGQAAAADAAAAAAAAAAAAAAABAAAAAAAAB9AAAAAAAAAAAAAA" +
        "AAAAAAAAAAEAAAAAAAAAAAAAAAAAAAABAAAAAAAAAAAAAAAAAABAAAAAAUAAAADwAAAAAAFHbWRpYQAAACBtZGhk" +
        "AAAAAAAAAAAAAAAAAAAD6AAAB9BVxAAAAAAALWhkbHIAAAAAAAAAAHZpZGUAAAAAAAAAAAAAAABWaWRlb0hhbmRs" +
        "ZXIAAAAA8m1pbmYAAAAUdm1oZAAAAAEAAAAAAAAAAAAAACRkaW5mAAAAHGRyZWYAAAAAAAAAAQAAAAx1cmwgAAAA" +
        "AQAAALJzdGJsAAAAZnN0c2QAAAAAAAAAAQAAAFZhdmMxAAAAAAAAAAEAAAAAAAAAAAAAAAAAAAAAAUAA8ABIAAAA" +
        "SAAAAAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAGP//AAAAEHN0dHMAAAAAAAAAAAAAABBz" +
        "dHNjAAAAAAAAAAAAAAAUc3RzegAAAAAAAAAAAAAAAAAAABBzdGNvAAAAAAAAAAAAAADidWR0YQAAANptZXRhAAAA" +
        "AAAAACFoZGxyAAAAAAAAAABtZGlyYXBwbAAAAAAAAAAAAAAAAK1pbHN0AAAAJ6luYW0AAAAfZGF0YQAAAAEAAAAA" +
        "R3Jhbml0IEFWIEYxNy44AAAAIalBUlQAAAAZZGF0YQAAAAEAAAAASkYgTWV5ZXJzAAAAJalhbGIAAAAdZGF0YQAA" +
        "AAEAAAAAQXNzZXRNZXRhZGF0YQAAABypZ2VuAAAAFGRhdGEAAAABAAAAAFRlc3QAAAAcqWRheQAAABRkYXRhAAAA" +
        "AQAAAAAyMDI2AAAACG1kYXQ=";
}
