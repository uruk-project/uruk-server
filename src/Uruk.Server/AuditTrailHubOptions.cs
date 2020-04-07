using System.ComponentModel.DataAnnotations;

namespace Uruk.Server
{
    public class AuditTrailHubOptions
    {
        public AuditTrailHubRegistry Registry { get; } = new AuditTrailHubRegistry();
    }
}
