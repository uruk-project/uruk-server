using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Uruk.Server
{
    public class AuditTrailHubMiddleware
    {
        private static readonly JsonEncodedText errJson = JsonEncodedText.Encode("err");
        private static readonly JsonEncodedText descriptionJson = JsonEncodedText.Encode("description");

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

            var response = context.Response;
            var readResult = await request.BodyReader.ReadAsync();
            if (await _auditTrailService.TryStoreAuditTrail(readResult.Buffer, out var error))
            {
                response.StatusCode = StatusCodes.Status202Accepted;
            }
            else
            {
                switch (error!.Status)
                {
                    case AuditTrailErrorStatus.BadRequest:
                        WriteBadRequestResponse(response, error);

                        break;

                    case AuditTrailErrorStatus.TooManyRequest:
                        WriteTooManyRequestResponse(response);
                        break;

                    default:
                        throw new InvalidOperationException();

                }
            }

            request.BodyReader.AdvanceTo(readResult.Buffer.End);
        }

        private static void WriteTooManyRequestResponse(HttpResponse response)
        {
            response.StatusCode = StatusCodes.Status429TooManyRequests;
            response.Headers["Retry-After"] = "60";
        }

        private static void WriteBadRequestResponse(HttpResponse response, AuditTrailError? error)
        {
            response.ContentType = "application/json";
            response.StatusCode = StatusCodes.Status400BadRequest;

            // invalid_request
            // invalid_key
            // authentication_failed
            ReusableUtf8JsonWriter reusableWriter = ReusableUtf8JsonWriter.Get(response.BodyWriter);
            try
            {
                var writer = reusableWriter.GetJsonWriter();
                writer.WriteStartObject();

                writer.WriteString(errJson, error!.Code);
                if (error.Description != null)
                {
                    writer.WriteString(descriptionJson, error.Description);
                }

                writer.WriteEndObject();
                writer.Flush();
            }
            finally
            {
                ReusableUtf8JsonWriter.Return(reusableWriter);
            }
        }
    }
}
