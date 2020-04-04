using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Uruk.Server
{
    public class AuditTrailStorageBackgroundService : BackgroundService
    {
        private readonly IAuditTrailSink _sink;
        private readonly IAuditTrailStore _store;
        private readonly ILogger<AuditTrailStorageBackgroundService> _logger;

        public AuditTrailStorageBackgroundService(IAuditTrailSink sink, IAuditTrailStore store, ILogger<AuditTrailStorageBackgroundService> logger)
        {
            _sink = sink;
            _store = store;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (await _sink.WaitToReadAsync(cancellationToken))
            {
                while (_sink.TryRead(out var record))
                {
                    await _store.StoreAsync(record);
                    _logger.LogInformation($"'{record.Token.Payload!.Jti}' has been recorded");
                    // TODO: Merkle tree
                }
            }
        }
    }
}
