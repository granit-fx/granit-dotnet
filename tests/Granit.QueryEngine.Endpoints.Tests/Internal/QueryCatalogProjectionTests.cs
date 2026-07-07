using Granit.QueryEngine.Endpoints.Dtos;
using Granit.QueryEngine.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Endpoints.Tests.Internal;

public sealed class QueryCatalogProjectionTests
{
    private static string ByName(IQueryDefinitionDescriptor d) => d.Name;

    [Fact]
    public void Project_resolves_base_path_when_the_entity_type_is_routed()
    {
        IReadOnlyList<QueryCatalogEntryResponse> entries = QueryCatalogProjection.Project(
            [new FakeDescriptor("Acme.Patients", typeof(Patient))],
            new Dictionary<Type, string> { [typeof(Patient)] = "/api/patients" },
            ByName);

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
            new Dictionary<Type, string>(),
            ByName);

        entries.ShouldHaveSingleItem().BasePath.ShouldBeNull();
    }

    [Fact]
    public void Project_uses_the_label_key_resolver_for_the_label_key()
    {
        IReadOnlyList<QueryCatalogEntryResponse> entries = QueryCatalogProjection.Project(
            [new FakeDescriptor("Acme.Patients", typeof(Patient))],
            new Dictionary<Type, string>(),
            d => $"Entity:{d.Name}");

        entries.ShouldHaveSingleItem().LabelKey.ShouldBe("Entity:Acme.Patients");
    }

    [Fact]
    public void Project_preserves_the_input_order()
    {
        IReadOnlyList<QueryCatalogEntryResponse> entries = QueryCatalogProjection.Project(
            [
                new FakeDescriptor("Acme.Appointments", typeof(Appointment)),
                new FakeDescriptor("Acme.Doctors", typeof(Doctor)),
            ],
            new Dictionary<Type, string>(),
            ByName);

        entries.Select(e => e.Name).ShouldBe(["Acme.Appointments", "Acme.Doctors"]);
    }

    [Fact]
    public void Project_carries_the_owning_module()
    {
        IReadOnlyList<QueryCatalogEntryResponse> entries = QueryCatalogProjection.Project(
            [new FakeDescriptor("Acme.Patients", typeof(Patient), ModuleName: "Acme")],
            new Dictionary<Type, string>(),
            ByName);

        entries.ShouldHaveSingleItem().ModuleName.ShouldBe("Acme");
    }

    private sealed record FakeDescriptor(string Name, Type EntityType, string ModuleName = "Acme")
        : IQueryDefinitionDescriptor;

    private sealed class Patient;

    private sealed class Doctor;

    private sealed class Appointment;
}
