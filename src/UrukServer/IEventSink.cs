using System.Threading;
using System.Threading.Tasks;

namespace UrukServer
{
    public interface IEventSink
    {
        public bool TryWrite(Event @event);

        public Task Flush(CancellationToken cancellationToken);
    }
}
