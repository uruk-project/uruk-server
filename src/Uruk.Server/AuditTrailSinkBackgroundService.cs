using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Uruk.Server
{
    public class AuditTrailSinkBackgroundService : BackgroundService
    {
        private readonly IAuditTrailSink _eventSink;

        public AuditTrailSinkBackgroundService(IAuditTrailSink eventSink)
        {
            _eventSink = eventSink;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return _eventSink.Flush(stoppingToken);
        }
    }
}
