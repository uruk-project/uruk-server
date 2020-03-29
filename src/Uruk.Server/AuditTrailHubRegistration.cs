using System;
using JsonWebToken;

namespace Uruk.Server
{
    public class AuditTrailHubRegistration
    {
        public AuditTrailHubRegistration(string clientId, SignatureAlgorithm signatureAlgorithm, Jwk jwk)
        {
            ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
            Jwk = jwk ?? throw new ArgumentNullException(nameof(jwk));
            SignatureAlgorithm = signatureAlgorithm ?? throw new ArgumentNullException(nameof(signatureAlgorithm));
        }

        public AuditTrailHubRegistration(string clientId, SignatureAlgorithm signatureAlgorithm, string jwksUri)
        {
            ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
            JwksUri = jwksUri ?? throw new ArgumentNullException(nameof(jwksUri));
            SignatureAlgorithm = signatureAlgorithm ?? throw new ArgumentNullException(nameof(signatureAlgorithm));
        }

        public string ClientId { get; }

        public string? JwksUri { get; }

        public Jwk? Jwk { get; }

        public SignatureAlgorithm SignatureAlgorithm { get; }

        public TokenValidationPolicy BuildPolicy(string audience)
        {
            var builder = new TokenValidationPolicyBuilder()
                .RequireSecurityEventToken()
                .RequireAudience(audience);

            if (Jwk != null)
            {
                builder.RequireSignature(Jwk, SignatureAlgorithm);
            }
            else if (JwksUri != null)
            {
                builder.RequireSignature(JwksUri, SignatureAlgorithm);
            }

            return builder.Build();
        }
    }
}