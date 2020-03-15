using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace UrukServer
{
    public class EventSinkBackgroundService : BackgroundService
    {
        private readonly IEventSink _eventSink;

        public EventSinkBackgroundService(IEventSink eventSink)
        {
            _eventSink = eventSink;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return _eventSink.Flush(stoppingToken);
        }
    }
}
