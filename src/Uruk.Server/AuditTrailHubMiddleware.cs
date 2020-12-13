﻿using System;
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

            //context.Response.StatusCode = StatusCodes.Status403Forbidden;
            //context.Response.ContentType = "application/json";
            //ReusableUtf8JsonWriter reusableWriter = ReusableUtf8JsonWriter.Get(context.Response.BodyWriter);
            //try
            //{
            //    var writer = reusableWriter.GetJsonWriter();
            //    writer.WriteStartObject();
            //    writer.WriteString(errJson, accessDeniedJson);
            //    writer.WriteEndObject();
            //    writer.Flush();
            //    return;
            //}
            //finally
            //{
            //    ReusableUtf8JsonWriter.Return(reusableWriter);
            //}

            var readResult = await request.BodyReader.ReadAsync();
            var response = await _auditTrailService.TryStoreAuditTrail(readResult.Buffer);
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
                ReusableUtf8JsonWriter reusableWriter = ReusableUtf8JsonWriter.Get(context.Response.BodyWriter);
                try
                {
                    var writer = reusableWriter.GetJsonWriter();
                    writer.WriteStartObject();

                    writer.WriteString(errJson, response.Error);
                    if (response.Description != null)
                    {
                        writer.WriteString(descriptionJson, response.Description);
                    }

                    writer.WriteEndObject();
                    writer.Flush();
                }
                finally
                {
                    ReusableUtf8JsonWriter.Return(reusableWriter);
                }
            }

            request.BodyReader.AdvanceTo(readResult.Buffer.End);
        }

        private static void AuthenticationFailed(HttpContext context)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            ReusableUtf8JsonWriter reusableWriter = ReusableUtf8JsonWriter.Get(context.Response.BodyWriter);
            try
            {
                var writer = reusableWriter.GetJsonWriter();
                writer.WriteStartObject();
                writer.WriteString(errJson, authenticationFailedJson);
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
