using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JsonWebToken;
using Microsoft.Extensions.Options;
using Xunit;

namespace Uruk.Server.Tests
{
    public class AuditTrailHubServiceTests
    {
        private static readonly byte[] ValidToken = Encoding.UTF8.GetBytes("eyJhbGciOiJIUzI1NiIsInR5cCI6InNlY2V2ZW50K2p3dCIsImtpZCI6ImtleTEifQ.eyJpc3MiOiJodHRwczovL2lkcC5leGFtcGxlLmNvbS8iLCJqdGkiOiI3NTZFNjk3MTc1NjUyMDY5NjQ2NTZFNzQ2OTY2Njk2NTcyIiwiaWF0IjoxNTA4MTg0ODQ1LCJhdWQiOiI2MzZDNjk2NTZFNzQ1RjY5NjQiLCJldmVudHMiOnsiaHR0cHM6Ly9zY2hlbWFzLm9wZW5pZC5uZXQvc2VjZXZlbnQvcmlzYy9ldmVudC10eXBlL2FjY291bnQtZGlzYWJsZWQiOnsic3ViamVjdCI6eyJzdWJqZWN0X3R5cGUiOiJpc3Mtc3ViIiwiaXNzIjoiaHR0cHM6Ly9pZHAuZXhhbXBsZS5jb20vIiwic3ViIjoiNzM3NTYyNkE2NTYzNzQifSwicmVhc29uIjoiaGlqYWNraW5nIn19fQ.mXQbpdMkqB5trJcln2M89yaEfn57wS_dpWnhmui6OhE");

        [Fact]
        public async Task TryStoreToken_ValidToken_SinkOk_ReturnsSuccess()
        {
            var key = SymmetricJwk.FromBase64Url("YWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWE", false);
            key.Kid = JsonEncodedText.Encode("key1");
            var options = CreateOptions(key);
            var service = new AuditTrailHubService(new TestEventSink(response: true), options);

            var response = await service.TryStoreAuditTrail(new ReadOnlySequence<byte>(ValidToken));

            Assert.True(response.Succeeded);
            Assert.True(response.Error.EncodedUtf8Bytes.IsEmpty);
            Assert.Null(response.Description);
        }

        [Fact]
        public async Task TryStoreToken_ValidToken_SinkDown_ReturnsErrorResponse()
        {
            var key = SymmetricJwk.FromBase64Url("YWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWE", false);
            key.Kid = JsonEncodedText.Encode("key1");
            var options = CreateOptions(key);
            var service = new AuditTrailHubService(new TestEventSink(response: false), options);

            var response = await service.TryStoreAuditTrail(new ReadOnlySequence<byte>(ValidToken));

            Assert.False(response.Succeeded);
            Assert.False(response.Error.EncodedUtf8Bytes.IsEmpty);
            Assert.Null(response.Description);
        }

        [Fact]
        public async Task TryStoreToken_InvalidRequest_ReturnsInvalidRequest()
        {
            var options = CreateOptions();
            var service = new AuditTrailHubService(new TestEventSink(response: false), options);
            var response = await service.TryStoreAuditTrail(new ReadOnlySequence<byte>(new byte[0]));

            Assert.False(response.Succeeded);
            Assert.Equal(JsonEncodedText.Encode("invalid_request"), response.Error);
            Assert.NotNull(response.Description);
        }

        [Fact]
        public async Task TryStoreToken_BadIssuer_ReturnsInvalidKey()
        {
            var key = SymmetricJwk.FromBase64Url(new string('b', 16));
            key.Kid = JsonEncodedText.Encode("bad issuer");
            var options = CreateOptions(key);
            var service = new AuditTrailHubService(new TestEventSink(response: false), options);
            var response = await service.TryStoreAuditTrail(new ReadOnlySequence<byte>(ValidToken));

            Assert.False(response.Succeeded);
            Assert.Equal(JsonEncodedText.Encode("invalid_key"), response.Error);
            Assert.Null(response.Description);
        }

        [Fact]
        public async Task TryStoreToken_BadKey_ReturnsInvalidKey()
        {
            var key = SymmetricJwk.FromBase64Url(new string('b', 16));
            key.Kid = JsonEncodedText.Encode("bad key");
            var options = CreateOptions(key);
            var service = new AuditTrailHubService(new TestEventSink(response: false), options);
            var response = await service.TryStoreAuditTrail(new ReadOnlySequence<byte>(ValidToken));

            Assert.False(response.Succeeded);
            Assert.Equal(JsonEncodedText.Encode("invalid_key"), response.Error);
            Assert.Null(response.Description);
        }

        private static IOptions<AuditTrailHubOptions> CreateOptions(Jwk? key = null)
        {
            var registration = new AuditTrailHubRegistration("https://idp.example.com/", SignatureAlgorithm.HS256, key ?? SymmetricJwk.None);

            var options = new AuditTrailHubOptions();
            options.Registry.Add(registration);
            options.Policy = options.Registry.BuildPolicy("636C69656E745F6964");

            return Options.Create(options);
        }

        private class TestEventSink : IAuditTrailSink
        {
            private readonly bool _response;

            public TestEventSink(bool response)
            {
                _response = response;
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public bool TryRead(out AuditTrailRecord token)
            {
                token = default!;
                return _response;
            }

            public bool TryWrite(AuditTrailRecord token)
            {
                return _response;
            }

            public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken)
            {
                return new ValueTask<bool>(Task.FromResult(_response));
            }
        }
    }
}
