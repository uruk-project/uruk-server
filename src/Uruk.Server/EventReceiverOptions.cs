using System.Collections.Generic;

namespace Uruk.Server
{
    public class EventReceiverOptions
    {
        public List<EventReceiverRegistration> Registrations { get; } = new List<EventReceiverRegistration>();

        public string? Audience { get; set; }
    }
}
