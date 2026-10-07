using System.Reflection;
using NetArchTest.Rules;

namespace ArchitectureTests;

/// <summary>
/// Executable version of the Clean Architecture dependency rule (ADR-0002): source code dependencies
/// point inwards only, and services couple to each other through contracts only.
/// </summary>
public sealed class LayerDependencyTests
{
    public static TheoryData<string> Services => new() { "Catalog", "Ordering" };

    private static readonly string[] Frameworks =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "Npgsql",
        "Confluent.Kafka",
        "StackExchange.Redis",
    ];

    [Theory]
    [MemberData(nameof(Services))]
    public void Domain_DependsOnNothingButBuildingBlocksDomain(string service)
    {
        var result = Types.InAssembly(Layer(service, "Domain"))
            .ShouldNot()
            .HaveDependencyOnAny([$"{service}.Application", $"{service}.Infrastructure", $"{service}.Api", "BuildingBlocks.Application", "BuildingBlocks.Infrastructure", .. Frameworks])
            .GetResult();

        AssertSuccessful(result);
    }

    [Theory]
    [MemberData(nameof(Services))]
    public void Application_DoesNotDependOnInfrastructureOrFrameworks(string service)
    {
        var result = Types.InAssembly(Layer(service, "Application"))
            .ShouldNot()
            .HaveDependencyOnAny([$"{service}.Infrastructure", $"{service}.Api", "BuildingBlocks.Infrastructure", "BuildingBlocks.Web", .. Frameworks])
            .GetResult();

        AssertSuccessful(result);
    }

    [Theory]
    [InlineData("Catalog", "Ordering")]
    [InlineData("Ordering", "Catalog")]
    public void Services_OnlyCoupleThroughContracts(string service, string otherService)
    {
        string[] forbidden = [$"{otherService}.Domain", $"{otherService}.Application", $"{otherService}.Infrastructure", $"{otherService}.Api"];

        foreach (var layer in new[] { "Domain", "Application", "Infrastructure" })
        {
            AssertSuccessful(Types.InAssembly(Layer(service, layer)).ShouldNot().HaveDependencyOnAny(forbidden).GetResult());
        }
    }

    [Theory]
    [MemberData(nameof(Services))]
    public void Contracts_ArePlainMessages(string service)
    {
        var result = Types.InAssembly(Layer(service, "Contracts"))
            .ShouldNot()
            .HaveDependencyOnAny([$"{service}.Domain", $"{service}.Application", $"{service}.Infrastructure", "BuildingBlocks.Domain", .. Frameworks])
            .GetResult();

        AssertSuccessful(result);
    }

    [Theory]
    [MemberData(nameof(Services))]
    public void Handlers_AreInternalAndSealed(string service)
    {
        var result = Types.InAssembly(Layer(service, "Application"))
            .That()
            .HaveNameEndingWith("Handler")
            .Should()
            .BeSealed()
            .And()
            .NotBePublic()
            .GetResult();

        AssertSuccessful(result);
    }

    private static Assembly Layer(string service, string layer) => Assembly.Load($"{service}.{layer}");

    private static void AssertSuccessful(TestResult result) =>
        Assert.True(
            result.IsSuccessful,
            "Architecture violation in: " + string.Join(", ", result.FailingTypeNames ?? []));
}
