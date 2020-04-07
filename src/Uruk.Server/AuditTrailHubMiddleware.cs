using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Uruk.Server
{
    public class AuditTrailHubMiddleware
    {
        private static readonly JsonEncodedText errJson = JsonEncodedText.Encode("err");
        private static readonly JsonEncodedText descriptionJson = JsonEncodedText.Encode("description");
        private static readonly JsonEncodedText authenticationFailedJson = JsonEncodedText.Encode("authentication_failed");
        private static readonly JsonEncodedText accessDeniedJson = JsonEncodedText.Encode("access_denied");

        private readonly RequestDelegate _next;
        private readonly AuditTrailHubOptions _options;
        private readonly IAuditTrailHubService _auditTrailService;

        public AuditTrailHubMiddleware(RequestDelegate next, IOptions<AuditTrailHubOptions> options, IAuditTrailHubService auditTrailService)
        {
            _next = next;
            _options = options.Value;
            _auditTrailService = auditTrailService;
        }

        public async Task Invoke(HttpContext context)
        {
            var request = context.Request;
            if (!HttpMethods.IsPost(request.Method))
            {
                await _next(context);
                return;
            }

            if (!string.Equals(request.ContentType, "application/secevent+jwt", StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
                return;
            }

            if (!request.Headers.TryGetValue("Accept", out StringValues acceptHeaderValue) || !string.Equals(acceptHeaderValue, "application/json", StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status406NotAcceptable;
                return;
            }

            var authentication = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            if (!authentication.Succeeded || !authentication.Principal.Identity.IsAuthenticated)
            {
                AuthenticationFailed(context);
                return;
            }

            var clientIdClaim = authentication.Principal.FindFirst("client_id");
            if (clientIdClaim is null || clientIdClaim.Value is null)
            {
                AuthenticationFailed(context);
                return;
            }

            var clientId = clientIdClaim.Value;
            if (!_options.Registry.TryGet(clientId, out var registration))
            {
                await _options.Registry.Refresh(_options.Audience);
                if (!_options.Registry.TryGet(clientId, out registration))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json";
                    using Utf8JsonWriter writer = new Utf8JsonWriter(context.Response.BodyWriter);
                    writer.WriteStartObject();
                    writer.WriteString(errJson, accessDeniedJson);
                    writer.WriteEndObject();
                    writer.Flush();
                    return;
                }
            }

            var readResult = await request.BodyReader.ReadAsync();
            var response = await _auditTrailService.TryStoreAuditTrail(readResult.Buffer, registration);
            if (response.Succeeded)
            {
                context.Response.StatusCode = StatusCodes.Status202Accepted;
            }
            else
            {
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                // invalid_request
                // invalid_key
                // authentication_failed

                using Utf8JsonWriter writer = new Utf8JsonWriter(context.Response.BodyWriter);
                writer.WriteStartObject();

                writer.WriteString(errJson, response.Error);
                if (response.Description != null)
                {
                    writer.WriteString(descriptionJson, response.Description);
                }

                writer.WriteEndObject();
                writer.Flush();
            }

            request.BodyReader.AdvanceTo(readResult.Buffer.End);
        }

        private static void AuthenticationFailed(HttpContext context)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            Utf8JsonWriter writer = new Utf8JsonWriter(context.Response.BodyWriter);
            writer.WriteStartObject();
            writer.WriteString(errJson, authenticationFailedJson);
            writer.WriteEndObject();
            writer.Flush();
        }
    }
}
