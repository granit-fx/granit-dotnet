using Granit.BlobStorage;
using Granit.Documents.Domain;
using Granit.Documents.Endpoints.Documents.Dtos;

namespace Granit.Documents.Endpoints.Documents.Mapping;

/// <summary>Maps the <see cref="Document"/> aggregate and BlobStorage tickets to wire-shape DTOs.</summary>
internal static class DocumentMapper
{
    public static DocumentResponse ToResponse(this Document document) =>
        new(
            document.Id,
            document.FolderId,
            document.Name,
            document.Description,
            document.OwnerUserId,
            document.CurrentVersionId,
            document.Status.ToString(),
            document.TrashedAt);

    public static UploadTicketResponse ToResponse(this PresignedUploadTicket ticket) =>
        new(
            ticket.BlobId,
            ticket.UploadUrl,
            ticket.HttpMethod,
            ticket.ExpiresAt,
            ticket.RequiredHeaders);
}
