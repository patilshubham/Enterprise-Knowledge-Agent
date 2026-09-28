using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;

namespace EnterpriseKnowledgeAgent.Architecture.Tests;

/// <summary>
/// TASK-TST-02 (ASDD §4.4): enforces the modular-monolith's layering and
/// dependency-direction rules by reflecting over the compiled assemblies.
/// Layering (lowest to highest): Shared -> Domain -> Application ->
/// Infrastructure -> hosts (Api / AgentWorker / DocumentWorker). A lower
/// layer must never reference a higher layer, and the three hosts must
/// never reference each other directly.
/// </summary>
public class ModuleBoundaryTests
{
    private static readonly Assembly SharedAssembly = Assembly.Load("EnterpriseKnowledgeAgent.Shared");
    private static readonly Assembly DomainAssembly = Assembly.Load("EnterpriseKnowledgeAgent.Domain");
    private static readonly Assembly ApplicationAssembly = Assembly.Load("EnterpriseKnowledgeAgent.Application");
    private static readonly Assembly InfrastructureAssembly = Assembly.Load("EnterpriseKnowledgeAgent.Infrastructure");
    private static readonly Assembly ApiAssembly = Assembly.Load("EnterpriseKnowledgeAgent.Api");
    private static readonly Assembly AgentWorkerAssembly = Assembly.Load("EnterpriseKnowledgeAgent.AgentWorker");
    private static readonly Assembly DocumentWorkerAssembly = Assembly.Load("EnterpriseKnowledgeAgent.DocumentWorker");

    private const string SharedNamespace = "EnterpriseKnowledgeAgent.Shared";
    private const string DomainNamespace = "EnterpriseKnowledgeAgent.Domain";
    private const string ApplicationNamespace = "EnterpriseKnowledgeAgent.Application";
    private const string InfrastructureNamespace = "EnterpriseKnowledgeAgent.Infrastructure";
    private const string ApiNamespace = "EnterpriseKnowledgeAgent.Api";
    private const string AgentWorkerNamespace = "EnterpriseKnowledgeAgent.AgentWorker";
    private const string DocumentWorkerNamespace = "EnterpriseKnowledgeAgent.DocumentWorker";

    [Fact]
    public void Shared_ShouldNotDependOnAnyOtherModule()
    {
        var result = Types.InAssembly(SharedAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                DomainNamespace,
                ApplicationNamespace,
                InfrastructureNamespace,
                ApiNamespace,
                AgentWorkerNamespace,
                DocumentWorkerNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result, "Shared"));
    }

    [Fact]
    public void Domain_ShouldNotDependOnApplicationInfrastructureOrHosts()
    {
        var result = Types.InAssembly(DomainAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                ApplicationNamespace,
                InfrastructureNamespace,
                ApiNamespace,
                AgentWorkerNamespace,
                DocumentWorkerNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result, "Domain"));
    }

    [Fact]
    public void Application_ShouldNotDependOnInfrastructureOrHosts()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                InfrastructureNamespace,
                ApiNamespace,
                AgentWorkerNamespace,
                DocumentWorkerNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result, "Application"));
    }

    [Fact]
    public void Infrastructure_ShouldNotDependOnHosts()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                ApiNamespace,
                AgentWorkerNamespace,
                DocumentWorkerNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result, "Infrastructure"));
    }

    [Fact]
    public void Hosts_ShouldNotDependOnEachOther()
    {
        var apiResult = Types.InAssembly(ApiAssembly)
            .Should()
            .NotHaveDependencyOnAny(AgentWorkerNamespace, DocumentWorkerNamespace)
            .GetResult();
        apiResult.IsSuccessful.Should().BeTrue(BuildFailureMessage(apiResult, "Api"));

        var agentWorkerResult = Types.InAssembly(AgentWorkerAssembly)
            .Should()
            .NotHaveDependencyOnAny(ApiNamespace, DocumentWorkerNamespace)
            .GetResult();
        agentWorkerResult.IsSuccessful.Should().BeTrue(BuildFailureMessage(agentWorkerResult, "AgentWorker"));

        var documentWorkerResult = Types.InAssembly(DocumentWorkerAssembly)
            .Should()
            .NotHaveDependencyOnAny(ApiNamespace, AgentWorkerNamespace)
            .GetResult();
        documentWorkerResult.IsSuccessful.Should().BeTrue(BuildFailureMessage(documentWorkerResult, "DocumentWorker"));
    }

    private static string BuildFailureMessage(TestResult result, string moduleName)
    {
        var offendingTypes = result.FailingTypes is null
            ? string.Empty
            : string.Join(", ", result.FailingTypes.Select(t => t.FullName));

        return $"{moduleName} has a forbidden dependency. Offending types: {offendingTypes}";
    }
}
