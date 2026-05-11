using System;
using Granit.Browsing;
using Granit.Browsing.Capabilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Granit.Documents.Renditions.Pdf.Internal;

/// <summary>
/// Asserts at boot that an <see cref="IHeadlessBrowser"/> advertising the
/// <see cref="BrowserCapabilities.PdfViewerNative"/> capability is registered. Surfaces
/// an actionable error instead of failing on the first rendition attempt.
/// </summary>
internal interface IPdfRenditionStartupValidator
{
    void Validate();
}

internal sealed class PdfRenditionStartupValidator(IServiceProvider services) : IPdfRenditionStartupValidator
{
    public void Validate()
    {
        IHeadlessBrowser? browser = services.GetService<IHeadlessBrowser>();
        if (browser is null)
        {
            throw new InvalidOperationException(
                "Granit.Documents.Renditions.Pdf requires an IHeadlessBrowser registration. " +
                "Call AddGranitBrowsingPuppeteerSharp() or AddGranitBrowsingPlaywright() before " +
                "AddGranitDocumentsRenditionsPdf().");
        }
        if (!browser.Supports(BrowserCapabilities.PdfViewerNative))
        {
            throw new InvalidOperationException(
                $"The registered IHeadlessBrowser ('{browser.EngineName}') does not advertise " +
                "BrowserCapabilities.PdfViewerNative. Renditions.Pdf needs a Chromium-backed engine " +
                "with the native PDF viewer enabled.");
        }
        if (services.GetService<IPdfViewerCapability>() is null)
        {
            throw new InvalidOperationException(
                "IPdfViewerCapability is not registered. The browsing provider should have " +
                "registered it because the engine advertises BrowserCapabilities.PdfViewerNative.");
        }
    }
}

internal sealed class PdfRenditionStartupHostedService(IPdfRenditionStartupValidator validator) : IHostedService
{
    public System.Threading.Tasks.Task StartAsync(System.Threading.CancellationToken cancellationToken)
    {
        validator.Validate();
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public System.Threading.Tasks.Task StopAsync(System.Threading.CancellationToken cancellationToken) =>
        System.Threading.Tasks.Task.CompletedTask;
}
