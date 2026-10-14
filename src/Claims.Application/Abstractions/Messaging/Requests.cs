using Claims.Domain.Results;

namespace Claims.Application.Abstractions.Messaging;

public interface IRequest<TResponse>
    where TResponse : IOperationResult<TResponse>
{
}

public interface IBaseCommand
{
}

public interface ICommand : IRequest<Result>, IBaseCommand
{
}

public interface ICommand<TValue> : IRequest<Result<TValue>>, IBaseCommand
{
}

public interface IQuery<TValue> : IRequest<Result<TValue>>
{
}

public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IOperationResult<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand, Result>
    where TCommand : ICommand
{
}

public interface ICommandHandler<in TCommand, TValue> : IRequestHandler<TCommand, Result<TValue>>
    where TCommand : ICommand<TValue>
{
}

public interface IQueryHandler<in TQuery, TValue> : IRequestHandler<TQuery, Result<TValue>>
    where TQuery : IQuery<TValue>
{
}
