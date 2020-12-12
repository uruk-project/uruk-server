using System.ComponentModel.DataAnnotations;
using JsonWebToken;

namespace Uruk.Server
{
    public class AuditTrailHubOptions
    {
        public AuditTrailHubRegistry Registry { get; } = new AuditTrailHubRegistry();

        public TokenValidationPolicy Policy { get; set; }
    }
}
