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
            Assert.Throws<ArgumentNullException>(() => new AuditTrailHubRegistration(null, SignatureAlgorithm.HmacSha256, SymmetricJwk.GenerateKey(128)));
            Assert.Throws<ArgumentNullException>(() => new AuditTrailHubRegistration("client1", null, SymmetricJwk.GenerateKey(128)));
            Assert.Throws<ArgumentNullException>(() => new AuditTrailHubRegistration("client1", SignatureAlgorithm.HmacSha256, (Jwk)null));

            Assert.Throws<ArgumentNullException>(() => new AuditTrailHubRegistration(null, SignatureAlgorithm.HmacSha256, "https://demo.identityserver.io/.well-known/openid-configuration/jwks"));
            Assert.Throws<ArgumentNullException>(() => new AuditTrailHubRegistration("client1", null, "https://demo.identityserver.io/.well-known/openid-configuration/jwks"));
            Assert.Throws<ArgumentNullException>(() => new AuditTrailHubRegistration("client1", SignatureAlgorithm.HmacSha256, (string)null));
        }

        [Fact]
        public void Ctor_InitializeProperties()
        {
            var key = SymmetricJwk.GenerateKey(128);
            var reg1 = new AuditTrailHubRegistration("client1", SignatureAlgorithm.HmacSha256, key);
            Assert.Equal("client1", reg1.ClientId);
            Assert.Equal(key, reg1.Jwk);
            Assert.Null(reg1.JwksUri);
            Assert.Equal(SignatureAlgorithm.HmacSha256, reg1.SignatureAlgorithm);

            var reg2 = new AuditTrailHubRegistration("client2", SignatureAlgorithm.EcdsaSha256, "https://demo.identityserver.io/.well-known/openid-configuration/jwks");
            Assert.Equal("client2", reg2.ClientId);
            Assert.Null(reg2.Jwk);
            Assert.Equal("https://demo.identityserver.io/.well-known/openid-configuration/jwks", reg2.JwksUri);
            Assert.Equal(SignatureAlgorithm.EcdsaSha256, reg2.SignatureAlgorithm);
        }

        [Fact]
        public void BuildPolicy()
        {
            const int ValidateSignature = 0x01;
            const int ValidateAudience = 0x02;

            var reg1 = new AuditTrailHubRegistration("client1", SignatureAlgorithm.HmacSha256, SymmetricJwk.GenerateKey(128));
            reg1.ConfigurePolicy("uruk.example.com");
            Assert.Equal(ValidateSignature | ValidateAudience, reg1.Policy.ValidationControl);
            Assert.Single(reg1.Policy.RequiredAudiences, "uruk.example.com");
            Assert.NotEqual(SignatureValidationPolicy.NoSignature, reg1.Policy.SignatureValidationPolicy);
            Assert.NotEqual(SignatureValidationPolicy.IgnoreSignature, reg1.Policy.SignatureValidationPolicy);

            var reg2 = new AuditTrailHubRegistration("client1", SignatureAlgorithm.RsaSha256, "https://demo.identityserver.io/.well-known/openid-configuration/jwks");
            reg2.ConfigurePolicy("uruk.example.com");
            Assert.Equal(ValidateSignature | ValidateAudience, reg2.Policy.ValidationControl);
            Assert.Single(reg2.Policy.RequiredAudiences, "uruk.example.com");
            Assert.NotEqual(SignatureValidationPolicy.NoSignature, reg2.Policy.SignatureValidationPolicy);
            Assert.NotEqual(SignatureValidationPolicy.IgnoreSignature, reg2.Policy.SignatureValidationPolicy);
        }
    }
}
