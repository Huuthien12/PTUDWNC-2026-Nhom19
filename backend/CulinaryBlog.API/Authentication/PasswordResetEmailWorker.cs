using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Infrastructure.BackgroundJobs;

namespace CulinaryBlog.API.Authentication;

public sealed class PasswordResetEmailWorker(
    PasswordResetEmailQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<PasswordResetEmailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // In-memory by design: pending reset emails may be lost when the process stops.
        await foreach (var message in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IEmailService>()
                    .SendPasswordResetEmailAsync(message.Email, message.FullName, message.Token, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning("Password reset email delivery failed with {ExceptionType}.",
                    exception.GetType().Name);
            }
        }
    }
}
