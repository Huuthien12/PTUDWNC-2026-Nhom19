using CulinaryBlog.Application.Common.Behaviors;
using CulinaryBlog.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class LoggingBehaviorTests
{
    [Fact]
    public async Task Success_logs_type_but_not_request_values()
    {
        var logger = new RecordingLogger<LoggingBehavior<SensitiveCommand, string>>();
        var behavior = new LoggingBehavior<SensitiveCommand, string>(logger, new ManualTimeProvider());

        var result = await behavior.Handle(
            new SensitiveCommand("password-secret", "token-secret"),
            _ => Task.FromResult("ok"),
            CancellationToken.None);

        Assert.Equal("ok", result);
        Assert.Contains(nameof(SensitiveCommand), logger.AllText);
        Assert.DoesNotContain("password-secret", logger.AllText);
        Assert.DoesNotContain("token-secret", logger.AllText);
        Assert.Equal(LogLevel.Information, Assert.Single(logger.Entries).Level);
    }

    [Fact]
    public async Task Slow_success_is_warning_without_sleeping()
    {
        var logger = new RecordingLogger<LoggingBehavior<SensitiveCommand, string>>();
        var clock = new ManualTimeProvider();
        var behavior = new LoggingBehavior<SensitiveCommand, string>(logger, clock);

        await behavior.Handle(
            new SensitiveCommand("hidden", "hidden"),
            _ =>
            {
                clock.Advance(TimeSpan.FromMilliseconds(501));
                return Task.FromResult("ok");
            },
            CancellationToken.None);

        Assert.Equal(LogLevel.Warning, Assert.Single(logger.Entries).Level);
    }

    [Fact]
    public async Task Unexpected_exception_is_classified_and_rethrown_without_message()
    {
        var logger = new RecordingLogger<LoggingBehavior<SensitiveCommand, string>>();
        var behavior = new LoggingBehavior<SensitiveCommand, string>(logger, new ManualTimeProvider());
        var expected = new InvalidOperationException("connection-secret");

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(
            new SensitiveCommand("hidden", "hidden"),
            _ => Task.FromException<string>(expected),
            CancellationToken.None));

        Assert.Same(expected, actual);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("UnexpectedFailure", entry.Message);
        Assert.DoesNotContain("connection-secret", logger.AllText);
    }

    [Fact]
    public async Task Expected_exception_is_information_and_rethrown()
    {
        var logger = new RecordingLogger<LoggingBehavior<SensitiveCommand, string>>();
        var behavior = new LoggingBehavior<SensitiveCommand, string>(logger, new ManualTimeProvider());
        var expected = new BusinessRuleException("RULE", "user-input-secret");

        var actual = await Assert.ThrowsAsync<BusinessRuleException>(() => behavior.Handle(
            new SensitiveCommand("hidden", "hidden"),
            _ => Task.FromException<string>(expected),
            CancellationToken.None));

        Assert.Same(expected, actual);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("ExpectedFailure", entry.Message);
        Assert.DoesNotContain("user-input-secret", logger.AllText);
    }

    private sealed record SensitiveCommand(string Password, string Token) : IRequest<string>;
}
