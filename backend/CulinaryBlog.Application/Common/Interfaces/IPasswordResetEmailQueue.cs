namespace CulinaryBlog.Application.Common.Interfaces;

public sealed record PasswordResetEmailMessage(string Email, string FullName, string Token);

public interface IPasswordResetEmailQueue
{
    bool TryEnqueue(PasswordResetEmailMessage message);
}
