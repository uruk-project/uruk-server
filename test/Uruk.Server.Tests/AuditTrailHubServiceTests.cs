﻿using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JsonWebToken;
using Xunit;

namespace Uruk.Server.Tests
{
    public class AuditTrailHubServiceTests
    {
        private static readonly byte[] ValidToken = Encoding.UTF8.GetBytes("eyJhbGciOiJIUzI1NiIsInR5cCI6InNlY2V2ZW50K2p3dCIsImtpZCI6ImtleTEifQ.eyJpc3MiOiJodHRwczovL2lkcC5leGFtcGxlLmNvbS8iLCJqdGkiOiI3NTZFNjk3MTc1NjUyMDY5NjQ2NTZFNzQ2OTY2Njk2NTcyIiwiaWF0IjoxNTA4MTg0ODQ1LCJhdWQiOiI2MzZDNjk2NTZFNzQ1RjY5NjQiLCJldmVudHMiOnsiaHR0cHM6Ly9zY2hlbWFzLm9wZW5pZC5uZXQvc2VjZXZlbnQvcmlzYy9ldmVudC10eXBlL2FjY291bnQtZGlzYWJsZWQiOnsic3ViamVjdCI6eyJzdWJqZWN0X3R5cGUiOiJpc3Mtc3ViIiwiaXNzIjoiaHR0cHM6Ly9pZHAuZXhhbXBsZS5jb20vIiwic3ViIjoiNzM3NTYyNkE2NTYzNzQifSwicmVhc29uIjoiaGlqYWNraW5nIn19fQ.9Jd5SH14ZQp9I4nckqR_GdvhmLnQeZ1MJBu0nzBwQLg");

        [Fact]
        public async Task TryStoreToken_ValidToken_SinkOk_ReturnsSuccess()
        {
            var service = new AuditTrailHubService(new TestEventSink(response: true));
            var registration = CreateRegistration();

            var response = await service.TryStoreAuditTrail(new ReadOnlySequence<byte>(ValidToken), registration);

            Assert.True(response.Succeeded);
            Assert.True(response.Error.EncodedUtf8Bytes.IsEmpty);
            Assert.Null(response.Description);
        }

        [Fact]
        public async Task TryStoreToken_ValidToken_SinkDown_ReturnsErrorResponse()
        {
            var service = new AuditTrailHubService(new TestEventSink(response: false));
            var registration = CreateRegistration(Jwk.Empty);

            var response = await service.TryStoreAuditTrail(new ReadOnlySequence<byte>(ValidToken), registration);

            Assert.False(response.Succeeded);
            Assert.False(response.Error.EncodedUtf8Bytes.IsEmpty);
            Assert.Null(response.Description);
        }

        [Fact]
        public async Task TryStoreToken_InvalidRequest_ReturnsInvalidRequest()
        {
            var service = new AuditTrailHubService(new TestEventSink(response: false));
            var registration = CreateRegistration();
            var response = await service.TryStoreAuditTrail(new ReadOnlySequence<byte>(new byte[0]), registration);

            Assert.False(response.Succeeded);
            Assert.Equal(JsonEncodedText.Encode("invalid_request"), response.Error);
            Assert.NotNull(response.Description);
        }

        [Fact]
        public async Task TryStoreToken_BadKey_ReturnsInvalidKey()
        {
            var service = new AuditTrailHubService(new TestEventSink(response: false)); ;
            var key = new SymmetricJwk(new string('b', 128));
            var registration = CreateRegistration(key);
            key.Kid = "bad key";
            var response = await service.TryStoreAuditTrail(new ReadOnlySequence<byte>(ValidToken), registration);

            Assert.False(response.Succeeded);
            Assert.Equal(JsonEncodedText.Encode("invalid_key"), response.Error);
            Assert.Null(response.Description);
        }

        private static AuditTrailHubRegistration CreateRegistration(Jwk? key = null)
        {
            var registration = new AuditTrailHubRegistration("client_id", SignatureAlgorithm.None, key ?? SymmetricJwk.Empty);
            registration.ConfigurePolicy("636C69656E745F6964");
            return registration;
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

            public  ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken)
            {
                return new ValueTask<bool>(Task.FromResult(_response));
            }
        }
    }
}
