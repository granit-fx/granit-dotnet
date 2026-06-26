using Granit.QueryEngine.Extensions;
using Granit.QueryEngine.Internal;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Abstractions.Tests;

public sealed class QueryDefinitionRegistryTests
{
    [Fact]
    public void GetAll_orders_by_name_ordinal()
    {
        QueryDefinitionRegistry registry = new(
        [
            new FakeDescriptor("Acme.Patients", typeof(Patient)),
            new FakeDescriptor("Acme.Doctors", typeof(Doctor)),
            new FakeDescriptor("Acme.Appointments", typeof(Appointment)),
        ]);

        registry.GetAll().Select(d => d.Name).ShouldBe([
            "Acme.Appointments",
            "Acme.Doctors",
            "Acme.Patients",
        ]);
    }

    [Fact]
    public void Find_returns_descriptor_when_registered()
    {
        QueryDefinitionRegistry registry = new(
            [new FakeDescriptor("Acme.Patients", typeof(Patient))]);

        IQueryDefinitionDescriptor? found = registry.Find("Acme.Patients");

        found.ShouldNotBeNull();
        found.EntityType.ShouldBe(typeof(Patient));
    }

    [Fact]
    public void Find_returns_null_when_not_registered()
    {
        QueryDefinitionRegistry registry = new(
            [new FakeDescriptor("Acme.Patients", typeof(Patient))]);

        registry.Find("Acme.Missing").ShouldBeNull();
    }

    [Fact]
    public void Find_throws_on_null_or_empty_name()
    {
        QueryDefinitionRegistry registry = new([]);

        Should.Throw<ArgumentException>(() => registry.Find(string.Empty));
        Should.Throw<ArgumentException>(() => registry.Find(null!));
    }

    [Fact]
    public void Idempotent_registration_yields_a_single_catalogue_entry()
    {
        // AddQueryDefinition is a no-op on the second call, so the same definition registered
        // twice must surface exactly one descriptor in the catalogue — never a duplicate that
        // would trip the by-name ToDictionary or show twice in a dropdown.
        ServiceCollection services = new();
        services.AddQueryDefinition<Patient, PatientQueryDefinition>();
        services.AddQueryDefinition<Patient, PatientQueryDefinition>();

        using ServiceProvider sp = services.BuildServiceProvider();
        QueryDefinitionRegistry registry = new(sp.GetServices<IQueryDefinitionDescriptor>());

        registry.GetAll().ShouldHaveSingleItem().Name.ShouldBe("Acme.Patients");
    }

    private sealed record FakeDescriptor(string Name, Type EntityType, Type? LocalizationResourceType = null)
        : IQueryDefinitionDescriptor;

    private sealed class Patient
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class Doctor;

    private sealed class Appointment;

    private sealed class PatientQueryDefinition : QueryDefinition<Patient>
    {
        public override string Name => "Acme.Patients";

        protected override void Configure(QueryDefinitionBuilder<Patient> builder) =>
            builder.Column(e => e.Name);
    }
}
