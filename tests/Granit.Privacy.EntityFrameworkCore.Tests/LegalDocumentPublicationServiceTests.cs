using Granit.Privacy.LegalAgreements.Domain;
using Shouldly;
using Xunit;

namespace Granit.Privacy.EntityFrameworkCore.Tests;

public sealed class LegalDocumentPublicationServiceTests
{
    [Fact]
    public void LegalDocument_Create_should_set_properties()
    {
        var id = Guid.NewGuid();
        var doc = LegalDocument.Create(id, "privacy-policy", "Privacy Policy", "Initial version", "Legal.PrivacyPolicy");

        doc.Id.ShouldBe(id);
        doc.DocumentId.ShouldBe("privacy-policy");
        doc.DisplayName.ShouldBe("Privacy Policy");
        doc.Description.ShouldBe("Initial version");
        doc.TemplateName.ShouldBe("Legal.PrivacyPolicy");
        doc.DocumentBlobId.ShouldBeNull();
    }

    [Fact]
    public void LegalDocument_UpdateDraft_should_update_metadata()
    {
        var doc = LegalDocument.Create(Guid.NewGuid(), "tos", "Terms", null, null);

        doc.UpdateDraft("Updated Terms", "Changed clause 3", "Legal.ToS", Guid.NewGuid());

        doc.DisplayName.ShouldBe("Updated Terms");
        doc.Description.ShouldBe("Changed clause 3");
        doc.TemplateName.ShouldBe("Legal.ToS");
        doc.DocumentBlobId.ShouldNotBeNull();
    }

    [Fact]
    public void LegalDocument_AttachDocument_should_set_blob_id()
    {
        var doc = LegalDocument.Create(Guid.NewGuid(), "tos", "Terms");
        var blobId = Guid.NewGuid();

        doc.AttachDocument(blobId);

        doc.DocumentBlobId.ShouldBe(blobId);
    }
}
