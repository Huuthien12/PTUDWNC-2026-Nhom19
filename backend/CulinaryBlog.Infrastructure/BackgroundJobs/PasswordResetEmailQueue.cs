using System.Threading.Channels;
using CulinaryBlog.Application.Common.Interfaces;

namespace CulinaryBlog.Infrastructure.BackgroundJobs;

public sealed class PasswordResetEmailQueue : IPasswordResetEmailQueue
{
    private readonly Channel<PasswordResetEmailMessage> _channel;

    public PasswordResetEmailQueue(int capacity = 100)
    {
        _channel = Channel.CreateBounded<PasswordResetEmailMessage>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        });
    }

    public bool TryEnqueue(PasswordResetEmailMessage message) => _channel.Writer.TryWrite(message);

    public IAsyncEnumerable<PasswordResetEmailMessage> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
