using Granit.Http.ODataExposure.Internal;
using Microsoft.OData.Edm;
using Shouldly;
using Xunit;

namespace Granit.Http.ODataExposure.Tests;

/// <summary>
/// Locks the EDM model shape produced by the convention-based builder — one
/// EntitySet per descriptor, EntityType columns mirror the public CLR
/// properties, key auto-detected on the <c>Id</c> property. These shape
/// guarantees feed Power BI / Excel directly via <c>$metadata</c>.
/// </summary>
public sealed class ODataEdmModelBuilderTests
{
    [Fact]
    public void Build_SingleEntitySet_RegistersInTheModel()
    {
        ODataEntitySetDescriptor descriptor = new(
            EntitySetName: "Invoices",
            EntityType: typeof(Invoice),
            QueryDefinitionType: typeof(string), // unused by the EDM builder
            RequiredPermission: null);

        IEdmModel model = ODataEdmModelBuilder.Build([descriptor]);

        IEdmEntitySet? set = model.EntityContainer.FindEntitySet("Invoices");
        set.ShouldNotBeNull();
        set!.EntityType.Name.ShouldBe(nameof(Invoice));
    }

    [Fact]
    public void Build_MultipleEntitySets_AllAppearInTheContainer()
    {
        ODataEntitySetDescriptor[] descriptors =
        [
            new("Invoices", typeof(Invoice), typeof(string), null),
            new("Customers", typeof(Customer), typeof(string), "OData.Test.Customers.Read"),
        ];

        IEdmModel model = ODataEdmModelBuilder.Build(descriptors);

        model.EntityContainer.FindEntitySet("Invoices").ShouldNotBeNull();
        model.EntityContainer.FindEntitySet("Customers").ShouldNotBeNull();
    }

    [Fact]
    public void Build_EntityType_ExposesPublicProperties()
    {
        ODataEntitySetDescriptor descriptor = new(
            "Invoices", typeof(Invoice), typeof(string), null);

        IEdmModel model = ODataEdmModelBuilder.Build([descriptor]);

        var entityType = (IEdmEntityType)model.EntityContainer.FindEntitySet("Invoices")!.EntityType;
        IReadOnlyList<string> propertyNames = [.. entityType.Properties().Select(p => p.Name)];

        propertyNames.ShouldContain(nameof(Invoice.Id));
        propertyNames.ShouldContain(nameof(Invoice.Number));
        propertyNames.ShouldContain(nameof(Invoice.Total));
    }

    [Fact]
    public void Build_EmptyDescriptorList_Throws()
    {
        Should.Throw<ArgumentException>(() => ODataEdmModelBuilder.Build([]));
    }

    [Fact]
    public void Build_NullDescriptors_Throws()
    {
        Should.Throw<ArgumentNullException>(() => ODataEdmModelBuilder.Build(null!));
    }

    private sealed class Invoice
    {
        public Guid Id { get; init; }
        public string Number { get; init; } = string.Empty;
        public decimal Total { get; init; }
    }

    private sealed class Customer
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }
}
