using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Uruk.Server
{
    public interface IAuditTrailHubService
    {
        public Task<bool> TryStoreAuditTrail(ReadOnlySequence<byte> buffer, [NotNullWhen(false)] out AuditTrailError? error ,  CancellationToken cancellationToken = default);
    }
}
