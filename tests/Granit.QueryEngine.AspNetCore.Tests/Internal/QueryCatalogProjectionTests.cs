using Granit.QueryEngine.AspNetCore.Dtos;
using Granit.QueryEngine.AspNetCore.Internal;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.AspNetCore.Tests.Internal;

public sealed class QueryCatalogProjectionTests
{
    [Fact]
    public void Project_resolves_base_path_when_the_entity_type_is_routed()
    {
        IReadOnlyList<QueryCatalogEntryResponse> entries = QueryCatalogProjection.Project(
            [new FakeDescriptor("Acme.Patients", typeof(Patient))],
            new Dictionary<Type, string> { [typeof(Patient)] = "/api/patients" });

        QueryCatalogEntryResponse entry = entries.ShouldHaveSingleItem();
        entry.Name.ShouldBe("Acme.Patients");
        entry.BasePath.ShouldBe("/api/patients");
    }

    [Fact]
    public void Project_emits_null_base_path_when_the_entity_type_is_not_routed()
    {
        // Registration and routing are decoupled: a definition can be registered without any
        // MapGranitQuery route. The projection must surface a null base path, never a forged URL.
        IReadOnlyList<QueryCatalogEntryResponse> entries = QueryCatalogProjection.Project(
            [new FakeDescriptor("Acme.Patients", typeof(Patient))],
            new Dictionary<Type, string>());

        entries.ShouldHaveSingleItem().BasePath.ShouldBeNull();
    }

    [Fact]
    public void Project_uses_the_name_as_the_label()
    {
        IReadOnlyList<QueryCatalogEntryResponse> entries = QueryCatalogProjection.Project(
            [new FakeDescriptor("Acme.Patients", typeof(Patient))],
            new Dictionary<Type, string>());

        entries.ShouldHaveSingleItem().Label.ShouldBe("Acme.Patients");
    }

    [Fact]
    public void Project_preserves_the_input_order()
    {
        IReadOnlyList<QueryCatalogEntryResponse> entries = QueryCatalogProjection.Project(
            [
                new FakeDescriptor("Acme.Appointments", typeof(Appointment)),
                new FakeDescriptor("Acme.Doctors", typeof(Doctor)),
            ],
            new Dictionary<Type, string>());

        entries.Select(e => e.Name).ShouldBe(["Acme.Appointments", "Acme.Doctors"]);
    }

    private sealed record FakeDescriptor(string Name, Type EntityType) : IQueryDefinitionDescriptor;

    private sealed class Patient;

    private sealed class Doctor;

    private sealed class Appointment;
}
