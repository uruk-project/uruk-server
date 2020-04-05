using System.Threading;
using System.Threading.Tasks;

namespace Uruk.Server
{
    public interface IAuditTrailStore
    {
        Task StoreAsync(AuditTrailRecord record, CancellationToken cancellationToken = default);
    }
}
