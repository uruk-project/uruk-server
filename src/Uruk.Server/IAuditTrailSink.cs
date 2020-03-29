using System.Threading;
using System.Threading.Tasks;

namespace Uruk.Server
{
    public interface IAuditTrailSink
    {
        public bool TryWrite(AuditTrailRecord @event);

        public Task Flush(CancellationToken cancellationToken);
    }
}
