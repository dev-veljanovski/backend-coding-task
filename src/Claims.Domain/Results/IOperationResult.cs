namespace Claims.Domain.Results;

public interface IOperationResult
{
    bool IsSuccess { get; }

    bool IsFailure { get; }

    IReadOnlyList<Error> Errors { get; }
}

public interface IOperationResult<TSelf> : IOperationResult
    where TSelf : IOperationResult<TSelf>
{
    static abstract TSelf Failure(IReadOnlyList<Error> errors);
}
