using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Uruk.Server
{
    internal class DefaultAuditTrailSink : IAuditTrailSink
    {
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
    }
}
