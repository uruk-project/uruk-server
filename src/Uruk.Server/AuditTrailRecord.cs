using System;
using JsonWebToken;

namespace Uruk.Server
{
    public class AuditTrailRecord
    {
        public AuditTrailRecord(byte[] raw, SecurityEventToken token, string clientId)
        {
            Raw = raw ?? throw new ArgumentNullException(nameof(raw));
            Token = token ?? throw new ArgumentNullException(nameof(token));
            ClienId = clientId;
        }

        public byte[] Raw { get; }

        public SecurityEventToken Token { get; }

        public string ClienId { get; }

        public string Issuer => Token.Issuer!;
    }
}
