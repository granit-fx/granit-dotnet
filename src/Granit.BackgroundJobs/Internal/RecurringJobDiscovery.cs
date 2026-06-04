using System.Reflection;
using Granit.BackgroundJobs.Domain;
using Granit.Reflection;

namespace Granit.BackgroundJobs.Internal;

/// <summary>
/// Scans assemblies for message types decorated with <see cref="RecurringJobAttribute"/>
/// and produces <see cref="RecurringJobRegistration"/> descriptors for seeding.
/// </summary>
internal static class RecurringJobDiscovery
{
    /// <summary>
    /// Returns all <see cref="RecurringJobRegistration"/> found in <paramref name="assemblies"/>.
    /// </summary>
    internal static IReadOnlyList<RecurringJobRegistration> Discover(IEnumerable<Assembly> assemblies)
    {
        List<RecurringJobRegistration> registrations = [];

        foreach (Assembly assembly in assemblies)
        {
            foreach (Type type in assembly.GetLoadableTypes())
            {
                RecurringJobAttribute? attr = type.GetCustomAttribute<RecurringJobAttribute>();
                if (attr is null)
                {
                    continue;
                }

                string? assemblyQualifiedName = type.AssemblyQualifiedName;
                if (assemblyQualifiedName is null)
                {
                    continue;
                }

                registrations.Add(new RecurringJobRegistration(
                    JobName: attr.Name,
                    CronExpression: attr.CronExpression,
                    MessageType: assemblyQualifiedName));
            }
        }

        return registrations;
    }
}
