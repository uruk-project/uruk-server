using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using JsonWebToken;

namespace Uruk.Server
{
    public class AuditTrailHubRegistry : IEnumerable<AuditTrailHubRegistration>
    {
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        private readonly List<AuditTrailHubRegistration> _registrations = new List<AuditTrailHubRegistration>();

        public void Add(AuditTrailHubRegistration registration)
            => _registrations.Add(registration);

        public TokenValidationPolicy BuildPolicy(string audience)
        {
            var builder = new TokenValidationPolicyBuilder();
            builder.RequireAudience(audience)
                .RequireSecEventToken();
          
            for (int i = 0; i < _registrations.Count; i++)
            {
                var registration = _registrations[i];
                registration.ConfigurePolicy(builder);
            }

            return builder.Build();
        }

        public IEnumerator<AuditTrailHubRegistration> GetEnumerator()
            => ((IEnumerable<AuditTrailHubRegistration>)_registrations).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => ((IEnumerable<AuditTrailHubRegistration>)_registrations).GetEnumerator();
    }

    public class AuditTrailHubRegistration
    {
        public AuditTrailHubRegistration(string issuer, SignatureAlgorithm signatureAlgorithm, Jwk jwk)
        {
            Issuer = issuer ?? throw new ArgumentNullException(nameof(issuer));
            Jwk = jwk ?? throw new ArgumentNullException(nameof(jwk));
            SignatureAlgorithm = signatureAlgorithm ?? throw new ArgumentNullException(nameof(signatureAlgorithm));
        }

        public AuditTrailHubRegistration(string issuer, SignatureAlgorithm signatureAlgorithm, string jwksUri)
        {
            Issuer = issuer ?? throw new ArgumentNullException(nameof(issuer));
            JwksUri = jwksUri ?? throw new ArgumentNullException(nameof(jwksUri));
            SignatureAlgorithm = signatureAlgorithm ?? throw new ArgumentNullException(nameof(signatureAlgorithm));
        }

        public string Issuer { get; }

        public string? JwksUri { get; }

        public Jwk? Jwk { get; }

        public SignatureAlgorithm SignatureAlgorithm { get; }

        public void ConfigurePolicy(TokenValidationPolicyBuilder builder)
        {
            if (Jwk != null)
            {
                builder.RequireSignature(Issuer, Jwk, SignatureAlgorithm);
            }
            else if (JwksUri != null)
            {
                builder.RequireSignature(Issuer, JwksUri, SignatureAlgorithm);
            }
        }
    }
}