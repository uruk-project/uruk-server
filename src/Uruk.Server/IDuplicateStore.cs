using System.Threading;
using System.Threading.Tasks;

namespace Uruk.Server
{
    public interface IDuplicateStore
    {
        public ValueTask<bool> TryAddAsync(AuditTrailRecord record, CancellationToken cancellationToken = default);
    }
}
