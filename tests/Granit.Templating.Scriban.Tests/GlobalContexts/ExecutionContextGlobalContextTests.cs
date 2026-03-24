using Granit.MultiTenancy;
using Granit.Templating.Scriban.GlobalContexts;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Templating.Scriban.Tests.GlobalContexts;

public sealed class ExecutionContextGlobalContextTests
{
    private static ExecutionContextGlobalContext CreateSut(IServiceProvider sp) =>
        new(sp);

    private static ServiceProvider BuildSp(ICurrentTenant? tenant = null)
    {
        ServiceCollection services = new();
        if (tenant is not null)
        {
            services.AddSingleton(tenant);
        }
        return services.BuildServiceProvider();
    }

    // -------------------------------------------------------------------------
    // ContextName
    // -------------------------------------------------------------------------

    [Fact]
    public void ContextName_Is_context()
    {
        ExecutionContextGlobalContext sut = CreateSut(BuildSp());
        sut.ContextName.ShouldBe("context");
    }

    // -------------------------------------------------------------------------
    // Resolve — tenant not registered
    // -------------------------------------------------------------------------

    [Fact]
    public void Resolve_WithNoTenantRegistered_ReturnsCultureAndEmptyTenantFields()
    {
        ExecutionContextGlobalContext sut = CreateSut(BuildSp());

        dynamic resolved = sut.Resolve();
        System.Type type = resolved.GetType();

        string culture = (string)type.GetProperty("culture")!.GetValue(resolved)!;
        string tenantId = (string)type.GetProperty("tenant_id")!.GetValue(resolved)!;
        string tenantName = (string)type.GetProperty("tenant_name")!.GetValue(resolved)!;

        culture.ShouldNotBeNull("culture must always be set (invariant culture has Name = \"\")");
        tenantId.ShouldBeEmpty();
        tenantName.ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // Resolve — tenant registered but unavailable (IsAvailable = false)
    // -------------------------------------------------------------------------

    [Fact]
    public void Resolve_WithUnavailableTenant_ReturnsEmptyTenantFields()
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);

        ExecutionContextGlobalContext sut = CreateSut(BuildSp(tenant));

        dynamic resolved = sut.Resolve();
        System.Type type = resolved.GetType();

        string tenantId = (string)type.GetProperty("tenant_id")!.GetValue(resolved)!;
        string tenantName = (string)type.GetProperty("tenant_name")!.GetValue(resolved)!;

        tenantId.ShouldBeEmpty();
        tenantName.ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // Resolve — tenant available with Id and Name
    // -------------------------------------------------------------------------

    [Fact]
    public void Resolve_WithAvailableTenant_ReturnsTenantIdAndName()
    {
        var id = Guid.NewGuid();
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(true);
        tenant.Id.Returns(id);
        tenant.Name.Returns("Hôpital Saint-Luc");

        ExecutionContextGlobalContext sut = CreateSut(BuildSp(tenant));

        dynamic resolved = sut.Resolve();
        System.Type type = resolved.GetType();

        string tenantId = (string)type.GetProperty("tenant_id")!.GetValue(resolved)!;
        string tenantName = (string)type.GetProperty("tenant_name")!.GetValue(resolved)!;

        tenantId.ShouldBe(id.ToString());
        tenantName.ShouldBe("Hôpital Saint-Luc");
    }

    // -------------------------------------------------------------------------
    // Resolve — tenant available but Id is null
    // -------------------------------------------------------------------------

    [Fact]
    public void Resolve_WithAvailableTenantButNullId_ReturnsEmptyTenantId()
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(true);
        tenant.Id.Returns((Guid?)null);
        tenant.Name.Returns("SomeTenant");

        ExecutionContextGlobalContext sut = CreateSut(BuildSp(tenant));

        dynamic resolved = sut.Resolve();
        string tenantId = (string)resolved.GetType().GetProperty("tenant_id")!.GetValue(resolved)!;

        tenantId.ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // Resolve — tenant available but Name is null
    // -------------------------------------------------------------------------

    [Fact]
    public void Resolve_WithAvailableTenantButNullName_ReturnsEmptyTenantName()
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(true);
        tenant.Id.Returns(Guid.NewGuid());
        tenant.Name.Returns((string?)null);

        ExecutionContextGlobalContext sut = CreateSut(BuildSp(tenant));

        dynamic resolved = sut.Resolve();
        string tenantName = (string)resolved.GetType().GetProperty("tenant_name")!.GetValue(resolved)!;

        tenantName.ShouldBeEmpty();
    }
}
