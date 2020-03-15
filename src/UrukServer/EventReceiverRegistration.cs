using System;
using JsonWebToken;

namespace UrukServer
{
    public class EventReceiverRegistration
    {
        public EventReceiverRegistration(string clientId, SignatureAlgorithm signatureAlgorithm, Jwk jwk)
        {
            ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
            Jwk = jwk ?? throw new ArgumentNullException(nameof(jwk));
            SignatureAlgorithm = signatureAlgorithm ?? throw new ArgumentNullException(nameof(signatureAlgorithm));
        }

        public EventReceiverRegistration(string clientId, SignatureAlgorithm signatureAlgorithm, string jwksUri)
        {
            ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
            JwksUri = jwksUri ?? throw new ArgumentNullException(nameof(jwksUri));
            SignatureAlgorithm = signatureAlgorithm ?? throw new ArgumentNullException(nameof(signatureAlgorithm));
        }

        public string ClientId { get; set; }

        public string? JwksUri { get; set; }

        public Jwk? Jwk { get; set; }

        public SignatureAlgorithm SignatureAlgorithm { get; set; }

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

        internal TokenValidationPolicy BuildPolicy(object audience)
        {
            throw new NotImplementedException();
        }
    }
}