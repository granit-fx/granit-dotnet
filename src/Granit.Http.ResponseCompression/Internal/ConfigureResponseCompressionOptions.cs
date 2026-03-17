using Granit.Http.ResponseCompression.Options;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Options;

namespace Granit.Http.ResponseCompression.Internal;

/// <summary>
/// Bridges <see cref="GranitResponseCompressionOptions"/> to ASP.NET Core
/// <see cref="ResponseCompressionOptions"/> by configuring compression providers,
/// MIME types, and HTTPS behavior.
/// </summary>
internal sealed class ConfigureResponseCompressionOptions(
    IOptions<GranitResponseCompressionOptions> granitOptions) : IConfigureOptions<ResponseCompressionOptions>
{
    /// <inheritdoc/>
    public void Configure(ResponseCompressionOptions options)
    {
        GranitResponseCompressionOptions granit = granitOptions.Value;

        options.EnableForHttps = granit.EnableForHttps;

        options.MimeTypes = [.. ResponseCompressionDefaults.MimeTypes, "image/svg+xml"];
        options.ExcludedMimeTypes = ["text/event-stream"];

        if (granit.EnableBrotli)
        {
            options.Providers.Add<BrotliCompressionProvider>();
        }

        if (granit.EnableGzip)
        {
            options.Providers.Add<GzipCompressionProvider>();
        }
    }
}
