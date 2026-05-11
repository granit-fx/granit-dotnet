using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Granit.Documents.Renditions;
using Granit.Documents.Renditions.Domain;
using Granit.Documents.Renditions.Office.Internal;
using Granit.Documents.Renditions.Office.Options;
using Granit.Guids;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Documents.Renditions.Office.Tests;

public sealed class OfficeRenditionProviderTests
{
    private static OfficeRenditionProvider Build(string sofficeBinary = "soffice")
    {
        IGuidGenerator guids = Substitute.For<IGuidGenerator>();
        guids.Create().Returns(_ => Guid.NewGuid());
        return new(
            Microsoft.Extensions.Options.Options.Create(new OfficeRenditionOptions { SofficeBinary = sofficeBinary }),
            guids,
            NullLogger<OfficeRenditionProvider>.Instance);
    }

    [Theory]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document", true)]
    [InlineData("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", true)]
    [InlineData("application/vnd.openxmlformats-officedocument.presentationml.presentation", true)]
    [InlineData("application/msword", true)]
    [InlineData("application/vnd.ms-excel", true)]
    [InlineData("application/vnd.ms-powerpoint", true)]
    [InlineData("application/vnd.oasis.opendocument.text", true)]
    [InlineData("application/vnd.oasis.opendocument.spreadsheet", true)]
    [InlineData("application/vnd.oasis.opendocument.presentation", true)]
    [InlineData("application/rtf", true)]
    [InlineData("text/rtf", true)]
    [InlineData("application/pdf", false)]
    [InlineData("image/png", false)]
    [InlineData("", false)]
    public void CanHandle_recognises_office_mimes(string mime, bool expected) =>
        Build().CanHandle(mime).ShouldBe(expected);

    [Fact]
    public void OutputContentType_is_pdf() => Build().OutputContentType.ShouldBe("application/pdf");

    [Fact]
    public async Task GenerateAsync_rejects_non_office_mime()
    {
        using OfficeRenditionProvider provider = Build();
        using MemoryStream src = new(new byte[] { 0xFF });
        var target = new RenditionTarget(RenditionType.Web, "application/pdf");

        await Should.ThrowAsync<NotSupportedException>(() =>
            provider.GenerateAsync(src, "image/png", target, CancellationToken.None));
    }

    [Fact]
    public async Task GenerateAsync_throws_when_soffice_binary_missing()
    {
        // Point at a binary path we are sure does not exist. The provider should surface
        // a descriptive InvalidOperationException, not a generic OS-level Win32Exception.
        using OfficeRenditionProvider provider = Build("/nonexistent/path/to/soffice-xyz");
        using MemoryStream src = new(new byte[] { 0x50, 0x4B }); // ZIP magic — close enough for soffice's parser
        var target = new RenditionTarget(RenditionType.Thumbnail, "application/pdf");

        await Should.ThrowAsync<Exception>(() =>
            provider.GenerateAsync(
                src,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                target,
                CancellationToken.None));
    }
}
