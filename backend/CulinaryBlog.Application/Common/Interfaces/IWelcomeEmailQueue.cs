namespace CulinaryBlog.Application.Common.Interfaces;

public interface IWelcomeEmailQueue
{
    Task EnqueueAsync(string email, string fullName, CancellationToken cancellationToken = default);
}

public interface IEmailService
{
    Task SendWelcomeEmailAsync(string email, string fullName, CancellationToken cancellationToken = default);
    Task SendPasswordResetEmailAsync(
        string email,
        string fullName,
        string token,
        CancellationToken cancellationToken = default);
}
