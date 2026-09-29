namespace CulinaryBlog.Application.Common.Interfaces;

public interface IWelcomeEmailQueue
{
    Task EnqueueAsync(string email, string fullName, CancellationToken cancellationToken = default);
}

public interface IEmailService
{
    Task SendWelcomeEmailAsync(string email, string fullName, CancellationToken cancellationToken = default);
}
