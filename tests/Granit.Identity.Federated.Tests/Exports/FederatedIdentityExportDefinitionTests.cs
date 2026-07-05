using Granit.DataExchange.Export;
using Granit.Identity.Federated.Exports;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Tests.Exports;

public sealed class FederatedIdentityExportDefinitionTests
{
    private static readonly FederatedIdentityExportDefinition Sut = new();
    private static IReadOnlyList<ExportFieldDescriptor> Fields =>
        Sut.GetFields();

    [Fact]
    public void Name_is_stable() =>
        Sut.Name.ShouldBe("Granit.Identity.Federated.FederatedIdentityExport");

    [Fact]
    public void HasComplexFields_is_false() =>
        ((IExportDefinitionDescriptor)Sut).HasComplexFields.ShouldBeFalse();

    [Fact]
    public void UserId_is_exported_as_scalar()
    {
        ExportFieldDescriptor field = Fields.Single(f => f.PropertyPath == "UserId");
        field.RequiresHierarchy.ShouldBeFalse();
    }

    [Fact]
    public void UserId_precedes_ExternalUserId()
    {
        int userIdOrder = Fields.Single(f => f.PropertyPath == "UserId").Order;
        int externalOrder = Fields.Single(f => f.PropertyPath == "ExternalUserId").Order;
        userIdOrder.ShouldBeLessThan(externalOrder);
    }

    [Fact]
    public void MetadataJson_is_exported_as_scalar()
    {
        ExportFieldDescriptor field = Fields.Single(f => f.PropertyPath == "MetadataJson");
        field.RequiresHierarchy.ShouldBeFalse();
    }

    [Theory]
    [InlineData("Username")]
    [InlineData("Email")]
    [InlineData("FirstName")]
    [InlineData("LastName")]
    [InlineData("EmailHash")]
    public void Encrypted_and_hash_fields_are_not_exported(string fieldName) =>
        Fields.ShouldNotContain(f => f.PropertyPath == fieldName);
}
