using System.Collections.Generic;

namespace Uruk.Server
{
    public class AuditTrailHubOptions
    {
        public List<AuditTrailHubRegistration> Registrations { get; } = new List<AuditTrailHubRegistration>();

        public string? Audience { get; set; }
    }
}
