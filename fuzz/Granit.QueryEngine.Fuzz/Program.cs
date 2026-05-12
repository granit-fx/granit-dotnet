using Granit.QueryEngine.AspNetCore.Binding;
using Microsoft.AspNetCore.Http;
using SharpFuzz;

// AFL++ persistent-mode harness. SharpFuzz instruments the QueryEngine assembly so
// AFL gets coverage feedback across iterations. We treat the input as a query string
// body (no leading '?') and feed it through the production binder.
//
// Any unhandled exception escaping BindAsync is a finding. We swallow the
// ArgumentException thrown by the QueryString constructor itself because malformed
// query strings are input-shape noise: the framework never sees them — Kestrel does.

const int MaxInputChars = 8192;

Fuzzer.Run((string raw) =>
{
    if (raw.Length == 0)
    {
        return;
    }

    if (raw.Length > MaxInputChars)
    {
        raw = raw[..MaxInputChars];
    }

    // Strip a leading '?' so QueryString.ctor (which requires it) gets a single one.
    string body = raw.StartsWith('?') ? raw[1..] : raw;
    string candidate = "?" + body;

    QueryString queryString;
    try
    {
        queryString = new QueryString(candidate);
    }
    catch (ArgumentException)
    {
        return;
    }

    DefaultHttpContext ctx = new();
    ctx.Request.QueryString = queryString;

    _ = QueryRequestBinder.BindAsync(ctx, null!).GetAwaiter().GetResult();
});
