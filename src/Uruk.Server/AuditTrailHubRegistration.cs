using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using JsonWebToken;

namespace Uruk.Server
{
    public class AuditTrailHubRegistry : IEnumerable<AuditTrailHubRegistration>
    {
        private readonly List<AuditTrailHubRegistration> _registrations = new List<AuditTrailHubRegistration>();
        private readonly Dictionary<string, AuditTrailHubRegistration> _registrationLookup = new Dictionary<string, AuditTrailHubRegistration>();

        public void Add(AuditTrailHubRegistration registration)
            => _registrations.Add(registration);

        public bool TryGet(string key, [NotNullWhen(true)] out AuditTrailHubRegistration? registration)
            => _registrationLookup.TryGetValue(key, out registration);

        public void Configure(string audience)
        {
            _registrationLookup.Clear();
            for (int i = 0; i < _registrations.Count; i++)
            {
                var registration = _registrations[i];
                registration.ConfigurePolicy(audience);
                _registrationLookup.Add(registration.ClientId, registration);
            }
        }

        public Task Refresh(string audience)
        {
            Configure(audience);
            return Task.CompletedTask;
        }

        public IEnumerator<AuditTrailHubRegistration> GetEnumerator()
            => ((IEnumerable<AuditTrailHubRegistration>)_registrations).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => ((IEnumerable<AuditTrailHubRegistration>)_registrations).GetEnumerator();
    }

    public class AuditTrailHubRegistration
    {
        private TokenValidationPolicy? _policy;

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

        public TokenValidationPolicy Policy
            => _policy
                ?? throw new InvalidOperationException($"You must call the method '{nameof(ConfigurePolicy)}' before using the {nameof(Policy)} property.");

        public void ConfigurePolicy(string audience)
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

            _policy = builder.Build();
        }
    }
}