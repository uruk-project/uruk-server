using System.Threading;
using System.Threading.Tasks;

namespace Uruk.Server
{
    public interface IEventSink
    {
        public bool TryWrite(Event @event);

        public Task Flush(CancellationToken cancellationToken);
    }
}
