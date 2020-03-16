using System;
using JsonWebToken;

namespace Uruk.Server
{
    public class Event
    {
        public Event(byte[] raw, SecurityEventToken token)
        {
            Raw = raw ?? throw new ArgumentNullException(nameof(raw));
            Token = token ?? throw new ArgumentNullException(nameof(token));
        }

        public byte[] Raw { get; }

        public SecurityEventToken Token { get; }
    }
}
