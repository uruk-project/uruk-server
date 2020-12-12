using System;
using System.Buffers;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JsonWebToken;
using Microsoft.Extensions.Options;

namespace Uruk.Server
{
    public class AuditTrailHubService : IAuditTrailHubService
    {
        private static readonly JsonEncodedText invalidRequestJson = JsonEncodedText.Encode("invalid_request");
        private static readonly JsonEncodedText invalidKeyJson = JsonEncodedText.Encode("invalid_key");

        private readonly IAuditTrailSink _sink;
        private readonly AuditTrailHubOptions _options;

        public AuditTrailHubService(IAuditTrailSink sink, IOptions<AuditTrailHubOptions> options)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _options = options.Value;
        }

        public Task<AuditTrailResponse> TryStoreAuditTrail(ReadOnlySequence<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Jwt.TryParse(buffer, _options.Policy, out var jwt))
            {
                var record = new AuditTrailRecord(buffer.ToArray(), jwt, jwt.Payload.TryGetClaim("iss", out var iss) ? iss.GetString() : "");
                if (!_sink.TryWrite(record))
                {
                    // throttle ?
                    return Task.FromResult(new AuditTrailResponse
                    {
                        Error = JsonEncodedText.Encode("An error occurred when adding the event to the queue.")
                    });
                }

                return Task.FromResult(new AuditTrailResponse { Succeeded = true });
            }
            else
            {
                var error = jwt.Error!;
                if ((error.Status & TokenValidationStatus.KeyError) == TokenValidationStatus.KeyError)
                {
                    return Task.FromResult(new AuditTrailResponse
                    {
                        Error = invalidKeyJson
                    });
                }
                else
                {
                    var description = jwt.Error!.Status switch
                    {
                        TokenValidationStatus.MalformedToken => "Malformed token.",
                        TokenValidationStatus.TokenReplayed => "Duplicated token.",
                        TokenValidationStatus.Expired => "Expired token.",
                        TokenValidationStatus.MissingEncryptionAlgorithm => "Missing encryption algorithm in the header.",
                        TokenValidationStatus.DecryptionFailed => "Unable to decrypt the token.",
                        TokenValidationStatus.NotYetValid => "The token is not yet valid.",
                        TokenValidationStatus.DecompressionFailed => "Unable to decompress the token.",
                        TokenValidationStatus.CriticalHeaderMissing => $"The critical header '{error.ErrorHeader}' is missing.",
                        TokenValidationStatus.CriticalHeaderUnsupported => $"The critical header '{error.ErrorHeader}' is not supported.",
                        TokenValidationStatus.InvalidClaim => $"The claim '{error.ErrorClaim}' is invalid.",
                        TokenValidationStatus.MissingClaim => $"The claim '{error.ErrorClaim}' is missing.",
                        TokenValidationStatus.InvalidHeader => $"The header '{error.ErrorHeader}' is invalid.",
                        TokenValidationStatus.MissingHeader => $"The header '{error.ErrorHeader}' is missing.",
                        _ => null
                    };

                    return Task.FromResult(new AuditTrailResponse
                    {
                        Error = invalidRequestJson,
                        Description = description
                    });
                }
            }
        }
    }
}
