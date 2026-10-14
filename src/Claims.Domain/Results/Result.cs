namespace Claims.Domain.Results;

public sealed class Result : IOperationResult<Result>
{
    private static readonly Result SuccessInstance = new([]);

    private Result(IReadOnlyList<Error> errors) => Errors = errors;

    public bool IsSuccess => Errors.Count == 0;

    public bool IsFailure => !IsSuccess;

    public IReadOnlyList<Error> Errors { get; }

    public static Result Success() => SuccessInstance;

    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result([error]);
    }

    public static Result Failure(IReadOnlyList<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Count == 0)
        {
            throw new ArgumentException("A failed result requires at least one error.", nameof(errors));
        }

        return new Result(errors);
    }

    public static implicit operator Result(Error error) => Failure(error);
}
