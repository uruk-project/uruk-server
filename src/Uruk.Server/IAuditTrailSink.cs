using System.Threading;
using System.Threading.Tasks;

namespace Uruk.Server
{
    public interface IAuditTrailSink
    {
        public bool TryWrite(AuditTrailRecord token);

        public Task StopAsync(CancellationToken cancellationToken);

        public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken);

        public bool TryRead(out AuditTrailRecord token);
    }
}
