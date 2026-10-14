using System.Reflection;
using Claims.Api.Controllers;
using Claims.Application.Abstractions.Messaging;
using Claims.Domain.Abstractions;
using Claims.Infrastructure.Persistence;
using NetArchTest.Rules;

namespace Claims.ArchitectureTests;

public sealed class LayerDependencyTests
{
    private static readonly Assembly Domain = typeof(IAggregateRoot).Assembly;
    private static readonly Assembly Application = typeof(ISender).Assembly;
    private static readonly Assembly Infrastructure = typeof(MongoOptions).Assembly;
    private static readonly Assembly Api = typeof(ApiControllerBase).Assembly;

    private const string DomainNamespace = "Claims.Domain";
    private const string ApplicationNamespace = "Claims.Application";
    private const string InfrastructureNamespace = "Claims.Infrastructure";
    private const string ApiNamespace = "Claims.Api";

    [Fact]
    public void Domain_DependsOnNothingButTheBaseClassLibrary()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                ApplicationNamespace,
                InfrastructureNamespace,
                ApiNamespace,
                "Microsoft.AspNetCore",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.Extensions",
                "MongoDB",
                "Azure",
                "FluentValidation")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Application_DoesNotDependOnInfrastructureOrPresentationOrFrameworks()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny(
                InfrastructureNamespace,
                ApiNamespace,
                "Microsoft.AspNetCore",
                "Microsoft.EntityFrameworkCore",
                "MongoDB",
                "Azure")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Infrastructure_DoesNotDependOnPresentation()
    {
        var result = Types.InAssembly(Infrastructure)
            .ShouldNot()
            .HaveDependencyOn(ApiNamespace)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Controllers_DoNotTalkToPersistenceDirectly()
    {
        var result = Types.InAssembly(Api)
            .That().Inherit(typeof(ApiControllerBase))
            .ShouldNot()
            .HaveDependencyOnAny(
                InfrastructureNamespace,
                "Claims.Domain.Claims.IClaimRepository",
                "Claims.Domain.Covers.ICoverRepository",
                "MongoDB",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void InfrastructureImplementations_AreHiddenBehindPorts()
    {
        var result = Types.InAssembly(Infrastructure)
            .That().HaveNameEndingWith("Repository")
            .Or().HaveNameEndingWith("ReadService")
            .Or().HaveNameEndingWith("UnitOfWork")
            .Should().NotBePublic()
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    private static string Describe(NetArchTest.Rules.TestResult result) =>
        result.IsSuccessful
            ? string.Empty
            : "Violating types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
