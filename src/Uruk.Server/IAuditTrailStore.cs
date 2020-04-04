using System.Threading.Tasks;

namespace Uruk.Server
{
    public interface IAuditTrailStore
    {
        Task StoreAsync(AuditTrailRecord record);

        public ValueTask<bool> CheckDuplicateAsync(string issuer, string id, string clientId);
    }
}
