using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JsonWebToken;
using Microsoft.Extensions.Options;

namespace Uruk.Server
{
    public class AuditTrailHubService : IAuditTrailHubService
    {
        private static readonly Task<bool> _trueTask = Task.FromResult(true);
        private static readonly Task<bool> _falseTask = Task.FromResult(false);

        private readonly IAuditTrailSink _sink;
        private readonly AuditTrailHubOptions _options;

        public AuditTrailHubService(IAuditTrailSink sink, IOptions<AuditTrailHubOptions> options)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _options = options.Value;
        }


        public Task<bool> TryStoreAuditTrail(ReadOnlySequence<byte> buffer, [NotNullWhen(false)] out AuditTrailError? error, CancellationToken cancellationToken = default)
        {
            Jwt? jwt = null;

            try
            {
                if (Jwt.TryParse(buffer, _options.Policy, out jwt))
                {
                    var record = new AuditTrailRecord(buffer.ToArray(), jwt, jwt.Payload!.TryGetClaim("iss", out var iss) ? iss.GetString()! : "");
                    if (_sink.TryWrite(record))
                    {
                        error = null;
                        return _trueTask;
                    }

                    error = AuditTrailError.TooManyRequest();
                    return _falseTask;
                }
                else
                {
                    var jwtError = jwt.Error!;
                    if ((jwtError.Status & TokenValidationStatus.KeyError) == TokenValidationStatus.KeyError)
                    {
                        error = AuditTrailError.InvalidKey();
                        return _falseTask;
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
                            TokenValidationStatus.CriticalHeaderMissing => $"The critical header '{jwtError.ErrorHeader}' is missing.",
                            TokenValidationStatus.CriticalHeaderUnsupported => $"The critical header '{jwtError.ErrorHeader}' is not supported.",
                            TokenValidationStatus.InvalidClaim => $"The claim '{jwtError.ErrorClaim}' is invalid.",
                            TokenValidationStatus.MissingClaim => $"The claim '{jwtError.ErrorClaim}' is missing.",
                            TokenValidationStatus.InvalidHeader => $"The header '{jwtError.ErrorHeader}' is invalid.",
                            TokenValidationStatus.MissingHeader => $"The header '{jwtError.ErrorHeader}' is missing.",
                            _ => null
                        };

                        error = AuditTrailError.InvalidRequest(description);
                        return _falseTask;
                    }
                }
            }
            finally
            {
                jwt?.Dispose();
            }
        }
    }
}
