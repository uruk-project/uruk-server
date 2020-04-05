using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace Uruk.Server
{
    public interface IAuditTrailHubService
    {
        public Task<AuditTrailResponse> TryStoreAuditTrail(ReadOnlySequence<byte> buffer, AuditTrailHubRegistration registration, CancellationToken cancellationToken = default);
    }
}
