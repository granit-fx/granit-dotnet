using Granit.CustomerBalance.Domain;
using Granit.CustomerBalance.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.CustomerBalance.EntityFrameworkCore.Tests;

public sealed class CustomerBalanceDbConfigurationTests
{
    [Fact]
    public void DbTablePrefix_ShouldBeCustomerBalance()
    {
        GranitCustomerBalanceDbProperties.DbTablePrefix.ShouldBe("customer_balance_");
    }

    [Fact]
    public void DbSchema_ShouldBeNull()
    {
        GranitCustomerBalanceDbProperties.DbSchema.ShouldBeNull();
    }

    [Fact]
    public void Module_ShouldBeSealed()
    {
        typeof(GranitCustomerBalanceEntityFrameworkCoreModule).IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void BalanceAccount_ShouldHavePrivateSetters_OnDeclaredProperties()
    {
        // Properties required by interfaces (IConcurrencyAware) need public setters for
        // interceptor injection — exclude them from the private-setter check.
        HashSet<string> interfaceRequired = [nameof(BalanceAccount.ConcurrencyStamp)];

        // Only check properties declared on BalanceAccount itself, not inherited from base classes.
        System.Reflection.PropertyInfo[] properties = typeof(BalanceAccount)
            .GetProperties(System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.DeclaredOnly);

        foreach (System.Reflection.PropertyInfo prop in properties)
        {
            if (interfaceRequired.Contains(prop.Name))
            {
                continue;
            }

            System.Reflection.MethodInfo? setter = prop.GetSetMethod(nonPublic: true);

            if (setter is not null)
            {
                setter.IsPublic.ShouldBeFalse(
                    $"Property '{prop.Name}' on BalanceAccount should not have a public setter.");
            }
        }
    }
}
