using System.Buffers;
using System.Threading.Tasks;
using JsonWebToken;

namespace Uruk.Server
{
    public interface IAuditTrailHubService
    {
        public Task<AuditTrailResponse> TryStoreAuditTrail(ReadOnlySequence<byte> buffer, TokenValidationPolicy policy);
    }
}
