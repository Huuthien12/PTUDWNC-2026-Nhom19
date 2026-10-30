using CulinaryBlog.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Common.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger,
    TimeProvider timeProvider) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly TimeSpan SlowThreshold = TimeSpan.FromMilliseconds(500);

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var startedAt = timeProvider.GetTimestamp();
        var requestType = typeof(TRequest).Name;

        try
        {
            var response = await next();
            var elapsed = timeProvider.GetElapsedTime(startedAt);
            if (elapsed > SlowThreshold)
            {
                logger.LogWarning(
                    "CQRS {RequestType} completed with {Outcome} in {ElapsedMilliseconds} ms",
                    requestType,
                    "Success",
                    elapsed.TotalMilliseconds);
            }
            else
            {
                logger.LogInformation(
                    "CQRS {RequestType} completed with {Outcome} in {ElapsedMilliseconds} ms",
                    requestType,
                    "Success",
                    elapsed.TotalMilliseconds);
            }

            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            LogFailure("Canceled", LogLevel.Information, null);
            throw;
        }
        catch (Exception exception)
        {
            LogFailure(IsExpected(exception) ? "ExpectedFailure" : "UnexpectedFailure",
                IsExpected(exception) ? LogLevel.Information : LogLevel.Warning,
                exception.GetType().Name);
            throw;
        }

        void LogFailure(string outcome, LogLevel level, string? exceptionType)
        {
            logger.Log(
                level,
                "CQRS {RequestType} completed with {Outcome} in {ElapsedMilliseconds} ms ({ExceptionType})",
                requestType,
                outcome,
                timeProvider.GetElapsedTime(startedAt).TotalMilliseconds,
                exceptionType);
        }
    }

    private static bool IsExpected(Exception exception) =>
        exception is ValidationException or DomainException or UnauthorizedAccessException;
}
