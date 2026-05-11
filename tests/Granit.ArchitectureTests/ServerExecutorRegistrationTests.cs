using Granit.ArchitectureTests.Internal;
using Granit.Entities;
using Granit.Entities.Actions;
using Granit.Entities.Actions.Execution;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Enforces story #1822 / ADR-056: every <see cref="EntityActionDescriptor"/>
/// with <see cref="EntityActionDescriptor.RequiresServerExecution"/> set MUST
/// declare a <see cref="EntityActionDescriptor.ServerExecutorType"/> that
/// implements <see cref="IEntityActionExecutor{TEntity}"/> for the entity's CLR
/// type. The matching descriptor type is captured by the
/// <c>EntityActionBuilder&lt;TEntity&gt;.ServerExecutor&lt;TExecutor&gt;()</c> fluent
/// extension; the archi test catches drift if a contributor pokes the
/// descriptor through a different path.
/// </summary>
public sealed class ServerExecutorRegistrationTests
{
    [Fact]
    public void Every_server_executed_action_must_declare_a_matching_IEntityActionExecutor_implementation()
    {
        List<IEntityDefinitionDescriptor> entities = EntityDefinitionScan.ScanEntityDefinitions();

        List<string> violations = [];

        foreach (IEntityDefinitionDescriptor entity in entities)
        {
            EntityDefinitionDescriptor descriptor = entity.Descriptor;
            foreach (EntityActionDescriptor action in descriptor.Actions)
            {
                if (!action.RequiresServerExecution)
                {
                    continue;
                }

                if (action.ServerExecutorType is null)
                {
                    violations.Add(
                        $"Action '{action.Name}' on entity '{descriptor.Name}' has RequiresServerExecution=true "
                        + "but ServerExecutorType is null. Use .ServerExecutor<TExecutor>() on the action builder.");
                    continue;
                }

                Type expectedInterface = typeof(IEntityActionExecutor<>).MakeGenericType(descriptor.EntityType);
                if (!expectedInterface.IsAssignableFrom(action.ServerExecutorType))
                {
                    violations.Add(
                        $"Action '{action.Name}' on entity '{descriptor.Name}' declares ServerExecutorType "
                        + $"'{action.ServerExecutorType.FullName}' which does not implement "
                        + $"IEntityActionExecutor<{descriptor.EntityType.Name}>. Fix the .ServerExecutor<>() type "
                        + "or align the executor's generic argument with the entity type.");
                }
            }
        }

        violations.ShouldBeEmpty(
            "ADR-056 / story #1822: every server-executed action must ship a registered IEntityActionExecutor<TEntity> "
            + "for the matching entity type. Violations:\n" + string.Join("\n", violations));
    }
}
