using System;
using JsonWebToken;

namespace Uruk.Server
{
    public class AuditTrailRecord
    {
        public AuditTrailRecord(ReadOnlyMemory<byte> raw, Jwt token, string clientId)
        {
            Raw = raw;
            Token = token ?? throw new ArgumentNullException(nameof(token));
            ClientId = clientId;
        }

        public ReadOnlyMemory<byte> Raw { get; }

        public Jwt Token { get; }

        public string ClientId { get; }

        public string Issuer => Token!.Payload![JwtClaimNames.Iss.EncodedUtf8Bytes].GetString()!;
    }
}
