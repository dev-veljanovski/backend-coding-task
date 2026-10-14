using Claims.Application.Abstractions.Messaging;
using Claims.Domain.Results;
using FluentValidation;

namespace Claims.Application.Behaviors;

internal sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IOperationResult<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var errors = new List<Error>();

        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            errors.AddRange(result.Errors.Select(failure => Error.Validation(
                code: failure.ErrorCode,
                message: failure.ErrorMessage,
                target: ToCamelCase(failure.PropertyName))));
        }

        return errors.Count > 0
            ? TResponse.Failure(errors)
            : await next().ConfigureAwait(false);
    }

    private static string ToCamelCase(string propertyName) =>
        string.IsNullOrEmpty(propertyName) || char.IsLower(propertyName[0])
            ? propertyName
            : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
}
