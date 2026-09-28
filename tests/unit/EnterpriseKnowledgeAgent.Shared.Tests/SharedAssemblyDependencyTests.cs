using System.Reflection;
using EnterpriseKnowledgeAgent.Shared.Correlation;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Shared.Tests;

/// <summary>
/// Guards the architectural constraint that the Shared library stays free of
/// infrastructure dependencies (no EF Core, no AWS SDK, no HTTP clients) so it
/// can be referenced by every layer, including the domain, without pulling in
/// unrelated concerns.
/// </summary>
public class SharedAssemblyDependencyTests
{
    private static readonly string[] ForbiddenAssemblyNameFragments =
    [
        "EntityFrameworkCore",
        "AWSSDK",
        "Npgsql",
    ];

    [Fact]
    public void SharedAssembly_ShouldNotReferenceInfrastructureAssemblies()
    {
        var sharedAssembly = typeof(CorrelationId).Assembly;

        var referencedAssemblyNames = sharedAssembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToList();

        var offenders = referencedAssemblyNames
            .Where(name => ForbiddenAssemblyNameFragments.Any(fragment =>
                name.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        offenders.Should().BeEmpty("the Shared library must remain dependency-free (no EF Core, no AWS SDK)");
    }
}
