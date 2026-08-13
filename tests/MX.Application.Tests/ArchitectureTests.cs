using System.Reflection;
using MX.Application;
using MX.Domain;

namespace MX.Application.Tests;

/// <summary>
/// Guards the inward dependency rule: Api -> Infrastructure -> Application -> Domain.
///
/// Project references alone do not prove this, because a reference added by mistake
/// still compiles. These tests read the emitted assembly metadata, so a violation
/// fails the build instead of silently eroding the layering.
///
/// Note: the C# compiler omits references that end up unused, so these assertions
/// are one-directional — they can only ever catch a dependency that is genuinely
/// being used. That is exactly the case we care about.
/// </summary>
public class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(DomainAssemblyMarker).Assembly;
    private static readonly Assembly Application = typeof(ApplicationAssemblyMarker).Assembly;

    private static string[] ReferencedNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToArray();

    [Fact]
    public void Reflection_probe_actually_sees_references()
    {
        // Positive control. Every one of the rules below asserts an *absence*, which
        // would also hold if GetReferencedAssemblies() silently returned nothing.
        // This proves the probe reads real metadata, so the empty results mean
        // "no violation" rather than "nothing was inspected".
        Assert.Contains("System.Runtime", ReferencedNames(Domain));
        Assert.Contains("System.Runtime", ReferencedNames(Application));
    }

    [Fact]
    public void Domain_depends_on_no_other_project()
    {
        var offenders = ReferencedNames(Domain)
            .Where(name => name.StartsWith("MX.", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Domain_does_not_depend_on_web_or_persistence_frameworks()
    {
        var offenders = ReferencedNames(Domain)
            .Where(name =>
                name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) ||
                name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ||
                name.StartsWith("Microsoft.Extensions", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure()
    {
        // The application layer declares the ports; infrastructure implements them.
        // A reference in this direction would invert the dependency rule.
        var offenders = ReferencedNames(Application)
            .Where(name => name.StartsWith("MX.Infrastructure", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Application_does_not_depend_on_the_api_layer()
    {
        var offenders = ReferencedNames(Application)
            .Where(name => name.StartsWith("MX.Api", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(offenders);
    }
}
