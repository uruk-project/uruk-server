using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Uruk.Server
{
    public class AuditTrailHubOptions
    {
        public AuditTrailHubRegistry Registry { get; } = new AuditTrailHubRegistry();

        [Required]
        public string Audience { get; set; }
    }
}
