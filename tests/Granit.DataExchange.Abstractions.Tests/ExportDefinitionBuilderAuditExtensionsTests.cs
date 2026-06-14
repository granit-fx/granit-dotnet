using Granit.DataExchange.Export;
using Granit.Domain;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Abstractions.Tests;

public sealed class ExportDefinitionBuilderAuditExtensionsTests
{
    private sealed class AuditedThing : ICreationAuditedObject, IModificationAuditedObject
    {
        public DateTimeOffset CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTimeOffset? ModifiedAt { get; set; }
        public string? ModifiedBy { get; set; }
    }

    private sealed class CreationOnlyThing : ICreationAuditedObject
    {
        public DateTimeOffset CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
    }

    private sealed class AuditedThingExport : ExportDefinition<AuditedThing>
    {
        public override string Name => "Test.AuditedThing";
        protected override void Configure(ExportDefinitionBuilder<AuditedThing> builder) => builder.IncludeAuditFields();
    }

    private sealed class CreationOnlyExport : ExportDefinition<CreationOnlyThing>
    {
        public override string Name => "Test.CreationOnly";
        protected override void Configure(ExportDefinitionBuilder<CreationOnlyThing> builder) => builder.IncludeCreationAuditFields();
    }

    [Fact]
    public void IncludeAuditFields_appends_the_four_audit_fields_with_roundtrip_timestamps()
    {
        IReadOnlyList<ExportFieldDescriptor> fields = new AuditedThingExport().GetFields();

        fields.Select(f => f.PropertyPath).ShouldBe(["CreatedAt", "CreatedBy", "ModifiedAt", "ModifiedBy"]);
        fields.Single(f => f.PropertyPath == "CreatedAt").Format.ShouldBe("O");
        fields.Single(f => f.PropertyPath == "ModifiedAt").Format.ShouldBe("O");
    }

    [Fact]
    public void IncludeCreationAuditFields_appends_only_the_creation_fields()
    {
        IReadOnlyList<ExportFieldDescriptor> fields = new CreationOnlyExport().GetFields();

        fields.Select(f => f.PropertyPath).ShouldBe(["CreatedAt", "CreatedBy"]);
    }
}
