using System.Reflection;
using Claims.Application.Abstractions.Events;
using Claims.Application.Abstractions.Messaging;
using Claims.Domain.Abstractions;
using FluentValidation;

namespace Claims.ArchitectureTests;

public sealed class DesignConventionTests
{
    private static readonly Assembly Domain = typeof(IAggregateRoot).Assembly;
    private static readonly Assembly Application = typeof(ISender).Assembly;

    public static TheoryData<Type> RequestHandlers() => new(ConcreteTypesImplementing(Application, typeof(IRequestHandler<,>)));

    public static TheoryData<Type> DomainEventHandlers() => new(ConcreteTypesImplementing(Application, typeof(IDomainEventHandler<>)));

    public static TheoryData<Type> Validators() => new(ConcreteTypesImplementing(Application, typeof(IValidator<>)));

    public static TheoryData<Type> DomainEvents() => new(
        Domain.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IDomainEvent).IsAssignableFrom(t)));

    public static TheoryData<Type> Aggregates() => new(
        Domain.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IAggregateRoot).IsAssignableFrom(t)));

    [Theory]
    [MemberData(nameof(RequestHandlers))]
    [MemberData(nameof(DomainEventHandlers))]
    [MemberData(nameof(Validators))]
    public void UseCaseInternals_AreSealedAndNotPublic(Type type)
    {
        type.IsSealed.ShouldBeTrue($"{type.Name} should be sealed");
        type.IsPublic.ShouldBeFalse($"{type.Name} should be internal; callers go through ISender");
    }

    [Fact]
    public void EveryRequest_HasExactlyOneHandler()
    {
        var requests = Application.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>)))
            .ToList();

        requests.ShouldNotBeEmpty();
        foreach (var request in requests)
        {
            var handlers = ConcreteTypesImplementing(Application, typeof(IRequestHandler<,>))
                .Where(h => h.GetInterfaces().Any(i =>
                    i.IsGenericType
                    && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)
                    && i.GetGenericArguments()[0] == request))
                .ToList();

            handlers.Count.ShouldBe(1, $"{request.Name} must have exactly one handler");
        }
    }

    [Fact]
    public void EveryCommand_HasAValidator()
    {
        var commands = Application.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IBaseCommand).IsAssignableFrom(t));

        foreach (var command in commands)
        {
            var validatorType = typeof(IValidator<>).MakeGenericType(command);
            ConcreteTypesImplementing(Application, typeof(IValidator<>))
                .Any(validatorType.IsAssignableFrom)
                .ShouldBeTrue($"{command.Name} has no validator");
        }
    }

    [Theory]
    [MemberData(nameof(DomainEvents))]
    public void DomainEvents_AreSealedImmutableRecordsNamedConsistently(Type type)
    {
        type.IsSealed.ShouldBeTrue();
        type.Name.ShouldEndWith("DomainEvent");
        type.GetProperties().ShouldAllBe(p => p.SetMethod == null || p.SetMethod.ReturnParameter.GetRequiredCustomModifiers().Length > 0);
    }

    [Theory]
    [MemberData(nameof(Aggregates))]
    public void Aggregates_AreSealedAndHaveNoPublicConstructors(Type type)
    {
        type.IsSealed.ShouldBeTrue();
        type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).ShouldBeEmpty(
            $"{type.Name} must be created through a factory method that enforces its invariants");
    }

    [Theory]
    [MemberData(nameof(Aggregates))]
    public void Aggregates_HaveNoPublicSetters(Type type)
    {
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod?.IsPublic == true)
            .ShouldBeEmpty($"{type.Name} must protect its state");
    }

    private static IEnumerable<Type> ConcreteTypesImplementing(Assembly assembly, Type openGenericInterface) =>
        assembly.GetTypes().Where(t =>
            t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
            && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == openGenericInterface));
}
