using System.Diagnostics;
using Claims.Application.Abstractions.Messaging;
using Claims.Application.Diagnostics;
using Claims.Domain.Results;
using Microsoft.Extensions.Logging;

namespace Claims.Application.Behaviors;

internal sealed class ObservabilityBehavior<TRequest, TResponse>(ILogger<ObservabilityBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IOperationResult<TResponse>
{
    private static readonly string RequestName = typeof(TRequest).Name;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        using var activity = ApplicationDiagnostics.ActivitySource.StartActivity(RequestName);
        var startedAt = Stopwatch.GetTimestamp();

        ObservabilityLog.Handling(logger, RequestName);

        try
        {
            var response = await next().ConfigureAwait(false);
            var elapsed = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            var outcome = response.IsSuccess ? "success" : "failure";

            activity?.SetTag("claims.request.outcome", outcome);
            Record(outcome, elapsed);

            if (response.IsSuccess)
            {
                ObservabilityLog.Handled(logger, RequestName, elapsed);
            }
            else
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Request rejected");
                ObservabilityLog.Rejected(logger, RequestName, elapsed, string.Join(", ", response.Errors.Select(e => e.Code)));
            }

            return response;
        }
        catch (Exception exception)
        {
            var elapsed = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.AddException(exception);
            Record("exception", elapsed);
            ObservabilityLog.Failed(logger, exception, RequestName, elapsed);
            throw;
        }
    }

    private static void Record(string outcome, double elapsedMilliseconds)
    {
        var tags = new TagList { { "request", RequestName }, { "outcome", outcome } };
        ApplicationDiagnostics.RequestsCounter.Add(1, tags);
        ApplicationDiagnostics.RequestDuration.Record(elapsedMilliseconds, tags);
    }
}

internal static partial class ObservabilityLog
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Handling {RequestName}")]
    public static partial void Handling(ILogger logger, string requestName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Handled {RequestName} in {ElapsedMilliseconds:0.0} ms")]
    public static partial void Handled(ILogger logger, string requestName, double elapsedMilliseconds);

    [LoggerMessage(Level = LogLevel.Information, Message = "Rejected {RequestName} in {ElapsedMilliseconds:0.0} ms with errors {ErrorCodes}")]
    public static partial void Rejected(ILogger logger, string requestName, double elapsedMilliseconds, string errorCodes);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed {RequestName} after {ElapsedMilliseconds:0.0} ms")]
    public static partial void Failed(ILogger logger, Exception exception, string requestName, double elapsedMilliseconds);
}
