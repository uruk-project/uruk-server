using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Uruk.Server
{
    internal class DefaultAuditTrailSink : IAuditTrailSink
    {
#if NETSTANDARD2_0 || NETSTANDARD2_1
        private readonly BlockingCollection<AuditTrailRecord> _channel = new BlockingCollection<AuditTrailRecord>();

        public bool TryWrite(AuditTrailRecord token)
        {
            return _channel.TryAdd(token);
        }

        public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new ValueTask<bool>(Task.FromCanceled<bool>(cancellationToken));
            }

            return new ValueTask<bool>(Task.FromResult(true));
        }

        public bool TryRead(out AuditTrailRecord token)
        {
            return _channel.TryTake(out token);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _channel.CompleteAdding();
            return Task.Delay(100);
        }
#else
        private readonly Channel<AuditTrailRecord> _channel = Channel.CreateUnbounded<AuditTrailRecord>();
        public bool TryWrite(AuditTrailRecord token)
        {
            return _channel.Writer.TryWrite(token);
        }

        public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken)
        {
            return _channel.Reader.WaitToReadAsync(cancellationToken);
        }

        public bool TryRead(out AuditTrailRecord token)
        {
            return _channel.Reader.TryRead(out token);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _channel.Writer.Complete();
            return _channel.Reader.Completion;
        }
#endif
    }
}
