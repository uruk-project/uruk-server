using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Uruk.Server
{
    internal class DefaultAuditTrailSink : IAuditTrailSink
    {
        private readonly ILogger<DefaultAuditTrailSink> _logger;
        private readonly Channel<AuditTrailRecord> _channel;

        public DefaultAuditTrailSink(ILogger<DefaultAuditTrailSink> logger)
        {
            _logger = logger;
            _channel = Channel.CreateUnbounded<AuditTrailRecord>();
        }

        public bool TryWrite(AuditTrailRecord record)
        {
            return _channel.Writer.TryWrite(record);
        }

        public Task Flush(CancellationToken cancellationToken)
        {
            Task consumer = Task.Factory.StartNew(async () => {
                while (await _channel.Reader.WaitToReadAsync())
                {
                    while (_channel.Reader.TryRead(out var @event))
                    {
                        // TODO: creates a logger message
                        _logger.LogInformation($"'{@event.Token.Payload!.Jti}' - {Encoding.UTF8.GetString(@event.Raw)} has been recorded");
                        // TODO: Merkle tree
                    }
                }
            });

            return consumer;
        }
    }
}
