using Granit.DataExchange.Excel.Internal.Import;
using Granit.DataExchange.Import.Parsing;
using SharpFuzz;

// AFL++ persistent-mode harness around the Sylvan.Data.Excel-backed parser. SharpFuzz
// instruments Granit.DataExchange.Excel (and the Sylvan.Data.Excel dependency, when
// present) so AFL gets coverage feedback across iterations.
//
// We treat the input stream as a raw .xlsx payload, run ExtractHeadersAsync, then drain
// ParseAsync. Any unhandled exception escaping the harness is a finding. We swallow
// InvalidDataException (System.IO.Compression — xlsx is a zip) and anything thrown
// from the Sylvan.Data.Excel namespace — those signal malformed input by design.

const int MaxInputBytes = 256 * 1024;

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

    var parser = new SylvanExcelFileParser();
    var options = new FileParsingOptions
    {
        MimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    };

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
    catch (InvalidDataException)
    {
        // System.IO.Compression — xlsx is a zip; malformed central directory is input-shape.
    }
    catch (FormatException)
    {
        // Numeric/structural malformations downstream of xlsx parsing.
    }
    catch (EndOfStreamException)
    {
        // Truncated zip / part stream — expected for fuzzed input.
    }
    catch (Exception ex) when (ex.GetType().Namespace?.StartsWith("Sylvan.Data.Excel", StringComparison.Ordinal) == true)
    {
        // Library-internal "not a valid xlsx" signals.
    }
});
