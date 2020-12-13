using System;
using JsonWebToken;
using Xunit;

namespace Uruk.Server.Tests
{
    public class AuditTrailRegistrationTests
    {
        [Fact]
        public void Ctor_ThrowException()
        {
            Assert.Throws<ArgumentNullException>(() => new AuditTrailHubRegistration(null!, SignatureAlgorithm.HS256, SymmetricJwk.GenerateKey(128)));
            Assert.Throws<ArgumentNullException>(() => new AuditTrailHubRegistration("client1", null!, SymmetricJwk.GenerateKey(128)));
            Assert.Throws<ArgumentNullException>(() => new AuditTrailHubRegistration("client1", SignatureAlgorithm.HS256, (Jwk)null!));

            Assert.Throws<ArgumentNullException>(() => new AuditTrailHubRegistration(null!, SignatureAlgorithm.HS256, "https://demo.identityserver.io/.well-known/openid-configuration/jwks"));
            Assert.Throws<ArgumentNullException>(() => new AuditTrailHubRegistration("client1", null!, "https://demo.identityserver.io/.well-known/openid-configuration/jwks"));
            Assert.Throws<ArgumentNullException>(() => new AuditTrailHubRegistration("client1", SignatureAlgorithm.HS256, (string)null!));
        }

        [Fact]
        public void Ctor_InitializeProperties()
        {
            var key = SymmetricJwk.GenerateKey(128);
            var reg1 = new AuditTrailHubRegistration("client1", SignatureAlgorithm.HS256, key);
            Assert.Equal("client1", reg1.Issuer);
            Assert.Equal(key, reg1.Jwk);
            Assert.Null(reg1.JwksUri);
            Assert.Equal(SignatureAlgorithm.HS256, reg1.SignatureAlgorithm);

            var reg2 = new AuditTrailHubRegistration("client2", SignatureAlgorithm.ES256, "https://demo.identityserver.io/.well-known/openid-configuration/jwks");
            Assert.Equal("client2", reg2.Issuer);
            Assert.Null(reg2.Jwk);
            Assert.Equal("https://demo.identityserver.io/.well-known/openid-configuration/jwks", reg2.JwksUri);
            Assert.Equal(SignatureAlgorithm.ES256, reg2.SignatureAlgorithm);
        }

        [Fact]
        public void BuildPolicy()
        {
            const int ValidateSignature = 0x01;
            const int ValidateAudience = 0x02;
            var registry = new AuditTrailHubRegistry();

            var reg1 = new AuditTrailHubRegistration("client1", SignatureAlgorithm.HS256, SymmetricJwk.GenerateKey(128));
            registry.Add(reg1);
            var policy = registry.BuildPolicy("uruk.example.com");
            Assert.Equal(ValidateSignature | ValidateAudience, policy.Control);
            Assert.Single(policy.RequiredAudiences, "uruk.example.com");

            var reg2 = new AuditTrailHubRegistration("client2", SignatureAlgorithm.RS256, "https://demo.identityserver.io/.well-known/openid-configuration/jwks");
            registry.Add(reg2);
            policy = registry.BuildPolicy("uruk.example.com");
            Assert.Equal(ValidateSignature | ValidateAudience, policy.Control);
            Assert.Single(policy.RequiredAudiences, "uruk.example.com");
        }
    }
}
