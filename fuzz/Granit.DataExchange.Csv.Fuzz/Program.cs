using System.Text;
using Granit.DataExchange.Csv.Internal.Import;
using Granit.DataExchange.Import.Parsing;
using SharpFuzz;

// AFL++ persistent-mode harness around the Sep-backed CSV parser. SharpFuzz instruments
// Granit.DataExchange.Csv (and the Sep dependency, when present) so AFL gets coverage
// feedback across iterations.
//
// We treat the input stream as a raw CSV payload, run ExtractHeadersAsync, then drain
// ParseAsync. Any unhandled exception escaping the harness is a finding. We swallow
// FormatException, InvalidDataException, and anything thrown from the Sep namespace —
// those signal malformed input by design, not bugs.

const int MaxInputBytes = 64 * 1024;

Fuzzer.Run((Stream input) =>
{
    using var copy = new MemoryStream();
    input.CopyTo(copy);
    if (copy.Length == 0)
    {
        return;
    }

    byte[] bytes = copy.Length > MaxInputBytes
        ? copy.GetBuffer().AsSpan(0, MaxInputBytes).ToArray()
        : copy.ToArray();

    var parser = new SepCsvFileParser();
    var options = new FileParsingOptions();

    try
    {
        using var headerStream = new MemoryStream(bytes, writable: false);
        _ = parser.ExtractHeadersAsync(headerStream, options).GetAwaiter().GetResult();

        using var rowStream = new MemoryStream(bytes, writable: false);
        IAsyncEnumerable<RawImportRow> rows = parser.ParseAsync(rowStream, options);
        IAsyncEnumerator<RawImportRow> enumerator = rows.GetAsyncEnumerator();
        try
        {
            while (enumerator.MoveNextAsync().AsTask().GetAwaiter().GetResult())
            {
                _ = enumerator.Current;
            }
        }
        finally
        {
            enumerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
    catch (FormatException)
    {
        // Malformed numeric/structural content — expected for fuzzed input.
    }
    catch (InvalidDataException)
    {
        // Sep surfaces malformed-row signals as InvalidDataException.
    }
    catch (Exception ex) when (ex.GetType().Namespace?.StartsWith("nietras.SeparatedValues", StringComparison.Ordinal) == true)
    {
        // Library-internal exceptions for malformed CSV are also input-shape noise.
    }
    catch (DecoderFallbackException)
    {
        // Bad encoding bytes — StreamReader rejects before any Granit logic.
    }
});
