using System;
using System.Collections.Generic;
using System.Text;
using JsonWebToken;
using Xunit;

namespace Uruk.Server.Tests
{
    public class EventReceiverRegistrationTests
    {
        [Fact]
        public void Ctor_ThrowException()
        {
            Assert.Throws<ArgumentNullException>(() => new EventReceiverRegistration(null, SignatureAlgorithm.HmacSha256, SymmetricJwk.GenerateKey(128)));
            Assert.Throws<ArgumentNullException>(() => new EventReceiverRegistration("client1", null, SymmetricJwk.GenerateKey(128)));
            Assert.Throws<ArgumentNullException>(() => new EventReceiverRegistration("client1", SignatureAlgorithm.HmacSha256, (Jwk)null));

            Assert.Throws<ArgumentNullException>(() => new EventReceiverRegistration(null, SignatureAlgorithm.HmacSha256, "https://demo.identityserver.io/.well-known/openid-configuration/jwks"));
            Assert.Throws<ArgumentNullException>(() => new EventReceiverRegistration("client1", null, "https://demo.identityserver.io/.well-known/openid-configuration/jwks"));
            Assert.Throws<ArgumentNullException>(() => new EventReceiverRegistration("client1", SignatureAlgorithm.HmacSha256, (string)null));
        }

        [Fact]
        public void Ctor_InitializeProperties()
        {
            var key = SymmetricJwk.GenerateKey(128);
            var reg1 = new EventReceiverRegistration("client1", SignatureAlgorithm.HmacSha256, key);
            Assert.Equal("client1", reg1.ClientId);
            Assert.Equal(key, reg1.Jwk);
            Assert.Null(reg1.JwksUri);
            Assert.Equal(SignatureAlgorithm.HmacSha256, reg1.SignatureAlgorithm);

            var reg2 = new EventReceiverRegistration("client2", SignatureAlgorithm.EcdsaSha256, "https://demo.identityserver.io/.well-known/openid-configuration/jwks");
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

            var reg1 = new EventReceiverRegistration("client1", SignatureAlgorithm.HmacSha256, SymmetricJwk.GenerateKey(128));
            var policy1 = reg1.BuildPolicy("uruk.example.com");
            Assert.Equal(ValidateSignature | ValidateAudience, policy1.ValidationControl);
            Assert.Single(policy1.RequiredAudiences, "uruk.example.com");
            Assert.NotEqual(SignatureValidationPolicy.NoSignature, policy1.SignatureValidationPolicy);
            Assert.NotEqual(SignatureValidationPolicy.IgnoreSignature, policy1.SignatureValidationPolicy);

            var reg2 = new EventReceiverRegistration("client1", SignatureAlgorithm.RsaSha256, "https://demo.identityserver.io/.well-known/openid-configuration/jwks");
            var policy2 = reg2.BuildPolicy("uruk.example.com");
            Assert.Equal(ValidateSignature | ValidateAudience, policy2.ValidationControl);
            Assert.Single(policy1.RequiredAudiences, "uruk.example.com");
            Assert.NotEqual(SignatureValidationPolicy.NoSignature, policy2.SignatureValidationPolicy);
            Assert.NotEqual(SignatureValidationPolicy.IgnoreSignature, policy2.SignatureValidationPolicy);
        }
    }
}
