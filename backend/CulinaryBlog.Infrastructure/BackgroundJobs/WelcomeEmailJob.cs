using CulinaryBlog.Application.Common.Interfaces;
using Hangfire;

namespace CulinaryBlog.Infrastructure.BackgroundJobs;

public sealed class WelcomeEmailJob(IEmailService emailService)
{
    [AutomaticRetry(Attempts = 3)]
    public Task SendAsync(string email, string fullName) =>
        emailService.SendWelcomeEmailAsync(email, fullName);
}
