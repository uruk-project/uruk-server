using System.Text.Json;

namespace Uruk.Server
{
    public class AuditTrailError
    {
        private static JsonEncodedText _invalidRequestJson = JsonEncodedText.Encode("invalid_request");
        private static JsonEncodedText _invalidKeyJson = JsonEncodedText.Encode("invalid_key");
        private static JsonEncodedText _invalidIssuerJson = JsonEncodedText.Encode("invalid_issuer");
        private static JsonEncodedText _invalidAudienceJson = JsonEncodedText.Encode("invalid_audience");
        private static JsonEncodedText _authenticationFailed = JsonEncodedText.Encode("authentication_failed");
        private static JsonEncodedText _accessDenied = JsonEncodedText.Encode("access_denied");
        private static JsonEncodedText _tooManyRequest = JsonEncodedText.Encode("too_many_request");

        private static readonly AuditTrailError _invalidRequestError = new AuditTrailError(_invalidRequestJson);
        private static readonly AuditTrailError _invalidKeyError = new AuditTrailError(_invalidKeyJson);

        private AuditTrailError(JsonEncodedText error, AuditTrailErrorStatus status = AuditTrailErrorStatus.BadRequest)
        {
            Code = error;
            Status = status;
        }

        private AuditTrailError(JsonEncodedText error, string? description, AuditTrailErrorStatus status = AuditTrailErrorStatus.BadRequest)
        {
            Code = error;
            Description = description;
            Status = status;
        }

        public static AuditTrailError InvalidRequest(string? description)
            => new AuditTrailError(_invalidRequestJson, description);

        public static AuditTrailError InvalidIssuer(string? description)
            => new AuditTrailError(_invalidIssuerJson, description);

        public static AuditTrailError InvalidAudience(string? description)
            => new AuditTrailError(_invalidAudienceJson, description);

        public static AuditTrailError AuthenticationFailed(string? description)
            => new AuditTrailError(_authenticationFailed, description);

        public static AuditTrailError AccessDenied(string? description)
            => new AuditTrailError(_accessDenied, description);

        public static AuditTrailError TooManyRequest()
            => new AuditTrailError(_tooManyRequest, AuditTrailErrorStatus.TooManyRequest);

        public static AuditTrailError InvalidKey()
            => _invalidKeyError;

        public JsonEncodedText Code { get; }

        public string? Description { get; }

        public AuditTrailErrorStatus Status { get; }
    }
}
