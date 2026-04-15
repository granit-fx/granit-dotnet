using Granit.DataExchange.Export;
using Granit.DataProtection;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Export;

public sealed class ExportPropertyFilterTests
{
    // ---- Simple types included ------------------------------------------

    [Fact]
    public void BuildFields_includes_simple_types()
    {
        IReadOnlyList<ExportFieldDescriptor> fields = ExportPropertyFilter.BuildFields(typeof(SimpleEntity));

        fields.ShouldContain(f => f.PropertyPath == "Name" && f.ClrTypeName == "String");
        fields.ShouldContain(f => f.PropertyPath == "Age" && f.ClrTypeName == "Int32");
        fields.ShouldContain(f => f.PropertyPath == "Id" && f.ClrTypeName == "Guid");
        fields.ShouldContain(f => f.PropertyPath == "IsActive" && f.ClrTypeName == "Boolean");
        fields.ShouldContain(f => f.PropertyPath == "CreatedAt" && f.ClrTypeName == "DateTimeOffset");
        fields.ShouldContain(f => f.PropertyPath == "BirthDate" && f.ClrTypeName == "DateOnly");
        fields.ShouldContain(f => f.PropertyPath == "Amount" && f.ClrTypeName == "Decimal");
    }

    [Fact]
    public void BuildFields_includes_nullable_types()
    {
        IReadOnlyList<ExportFieldDescriptor> fields = ExportPropertyFilter.BuildFields(typeof(NullableEntity));

        fields.ShouldContain(f => f.PropertyPath == "OptionalAge" && f.ClrTypeName == "Int32");
        fields.ShouldContain(f => f.PropertyPath == "OptionalId" && f.ClrTypeName == "Guid");
        fields.ShouldContain(f => f.PropertyPath == "OptionalDate" && f.ClrTypeName == "DateTimeOffset");
    }

    [Fact]
    public void BuildFields_includes_enum_types()
    {
        IReadOnlyList<ExportFieldDescriptor> fields = ExportPropertyFilter.BuildFields(typeof(EnumEntity));

        fields.ShouldContain(f => f.PropertyPath == "Status" && f.ClrTypeName == "TestStatus");
    }

    // ---- Excluded types -------------------------------------------------

    [Fact]
    public void BuildFields_excludes_collections()
    {
        IReadOnlyList<ExportFieldDescriptor> fields = ExportPropertyFilter.BuildFields(typeof(CollectionEntity));

        fields.ShouldNotContain(f => f.PropertyPath == "Tags");
        fields.ShouldNotContain(f => f.PropertyPath == "Items");
        fields.ShouldNotContain(f => f.PropertyPath == "ReadOnlyItems");
        fields.ShouldContain(f => f.PropertyPath == "Name"); // string is not excluded
    }

    [Fact]
    public void BuildFields_excludes_byte_array()
    {
        IReadOnlyList<ExportFieldDescriptor> fields = ExportPropertyFilter.BuildFields(typeof(BinaryEntity));

        fields.ShouldNotContain(f => f.PropertyPath == "Data");
        fields.ShouldContain(f => f.PropertyPath == "Name");
    }

    [Fact]
    public void BuildFields_excludes_infrastructure_properties()
    {
        IReadOnlyList<ExportFieldDescriptor> fields = ExportPropertyFilter.BuildFields(typeof(InfrastructureEntity));

        fields.ShouldNotContain(f => f.PropertyPath == "ConcurrencyStamp");
        fields.ShouldNotContain(f => f.PropertyPath == "SecurityStamp");
        fields.ShouldContain(f => f.PropertyPath == "Name");
    }

    // ---- SensitiveData attribute ----------------------------------------

    [Fact]
    public void BuildFields_excludes_sensitive_omit()
    {
        IReadOnlyList<ExportFieldDescriptor> fields = ExportPropertyFilter.BuildFields(typeof(SensitiveEntity));

        fields.ShouldNotContain(f => f.PropertyPath == "Secret");
    }

    [Fact]
    public void BuildFields_excludes_sensitive_restricted()
    {
        IReadOnlyList<ExportFieldDescriptor> fields = ExportPropertyFilter.BuildFields(typeof(SensitiveEntity));

        fields.ShouldNotContain(f => f.PropertyPath == "PasswordHash");
    }

    [Fact]
    public void BuildFields_includes_sensitive_internal()
    {
        IReadOnlyList<ExportFieldDescriptor> fields = ExportPropertyFilter.BuildFields(typeof(SensitiveEntity));

        fields.ShouldContain(f => f.PropertyPath == "DisplayName");
    }

    [Fact]
    public void BuildFields_includes_sensitive_mask()
    {
        IReadOnlyList<ExportFieldDescriptor> fields = ExportPropertyFilter.BuildFields(typeof(SensitiveEntity));

        fields.ShouldContain(f => f.PropertyPath == "Email");
    }

    // ---- AuditIgnore attribute (by name) --------------------------------

    [Fact]
    public void BuildFields_excludes_audit_ignored()
    {
        IReadOnlyList<ExportFieldDescriptor> fields = ExportPropertyFilter.BuildFields(typeof(AuditIgnoredEntity));

        fields.ShouldNotContain(f => f.PropertyPath == "InternalNotes");
        fields.ShouldContain(f => f.PropertyPath == "Name");
    }

    // ---- Order auto-incrementing ----------------------------------------

    [Fact]
    public void BuildFields_assigns_auto_incrementing_order()
    {
        IReadOnlyList<ExportFieldDescriptor> fields = ExportPropertyFilter.BuildFields(typeof(SimpleEntity));

        for (int i = 0; i < fields.Count; i++)
        {
            fields[i].Order.ShouldBe(i);
        }
    }

    // ---- Defaults -------------------------------------------------------

    [Fact]
    public void BuildFields_sets_defaults()
    {
        IReadOnlyList<ExportFieldDescriptor> fields = ExportPropertyFilter.BuildFields(typeof(SimpleEntity));

        foreach (ExportFieldDescriptor field in fields)
        {
            field.Header.ShouldBeNull();
            field.Format.ShouldBeNull();
            field.IsNavigation.ShouldBeFalse();
        }
    }

    // ---- Empty entity ---------------------------------------------------

    [Fact]
    public void BuildFields_returns_empty_for_no_exportable_properties()
    {
        IReadOnlyList<ExportFieldDescriptor> fields = ExportPropertyFilter.BuildFields(typeof(EmptyEntity));

        fields.ShouldBeEmpty();
    }

    // ---- Null argument --------------------------------------------------

    [Fact]
    public void BuildFields_throws_for_null_type()
    {
        Should.Throw<ArgumentNullException>(() => ExportPropertyFilter.BuildFields(null!));
    }

    // ---- Test helpers ---------------------------------------------------

    private sealed class SimpleEntity
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public bool IsActive { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateOnly BirthDate { get; set; }
        public decimal Amount { get; set; }
    }

    private sealed class NullableEntity
    {
        public int? OptionalAge { get; set; }
        public Guid? OptionalId { get; set; }
        public DateTimeOffset? OptionalDate { get; set; }
    }

    private sealed class EnumEntity
    {
        public TestStatus Status { get; set; }
    }

    private enum TestStatus { Active, Inactive }

    private sealed class CollectionEntity
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = [];
        public ICollection<int> Items { get; set; } = [];
        public IReadOnlyCollection<string> ReadOnlyItems { get; set; } = [];
    }

    private sealed class BinaryEntity
    {
        public string Name { get; set; } = string.Empty;
        public byte[] Data { get; set; } = [];
    }

    private sealed class InfrastructureEntity
    {
        public string Name { get; set; } = string.Empty;
        public string ConcurrencyStamp { get; set; } = string.Empty;
        public string SecurityStamp { get; set; } = string.Empty;
    }

    private sealed class SensitiveEntity
    {
        [SensitiveData(Mode = SensitiveDataMode.Omit)]
        public string Secret { get; set; } = string.Empty;

        [SensitiveData(Level = Sensitivity.Restricted)]
        public string PasswordHash { get; set; } = string.Empty;

        [SensitiveData(Level = Sensitivity.Internal)]
        public string DisplayName { get; set; } = string.Empty;

        [SensitiveData(Level = Sensitivity.Confidential, Mode = SensitiveDataMode.Mask)]
        public string Email { get; set; } = string.Empty;
    }

    // Test-only attribute matching AuditIgnoreAttribute by name
    [AttributeUsage(AttributeTargets.Property)]
    private sealed class AuditIgnoreAttribute : Attribute;

    private sealed class AuditIgnoredEntity
    {
        public string Name { get; set; } = string.Empty;

        [AuditIgnore]
        public string InternalNotes { get; set; } = string.Empty;
    }

    private sealed class EmptyEntity;
}
