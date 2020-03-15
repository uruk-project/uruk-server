using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using JsonWebToken;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace UrukServer
{
    public class EventReceiverMiddleware
    {
        private static readonly JsonEncodedText errJson = JsonEncodedText.Encode("err");
        private static readonly JsonEncodedText descriptionJson = JsonEncodedText.Encode("description");
        private static readonly JsonEncodedText authenticationFailedJson = JsonEncodedText.Encode("authentication_failed");
        private static readonly JsonEncodedText accessDeniedJson = JsonEncodedText.Encode("access_denied");

        private readonly RequestDelegate _next;
        private readonly EventReceiverOptions _options;
        private readonly Dictionary<string, TokenValidationPolicy> _registrations;
        private readonly IEventReceiverService _receiver;

        public EventReceiverMiddleware(RequestDelegate next, IOptions<EventReceiverOptions> options, IEventReceiverService receiver)
        {
            _next = next;
            _options = options.Value;
            _registrations = options.Value.Registrations.ToDictionary(v => v.ClientId, v => v.BuildPolicy(_options.Audience));
            _receiver = receiver;
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
                // TODO : bad content type
                context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
                return;
            }

            if (!request.Headers.TryGetValue("Accept", out StringValues acceptHeaderValue) || !string.Equals(acceptHeaderValue, "application/json", StringComparison.Ordinal))
            {
                // TODO : bad accept
                context.Response.StatusCode = StatusCodes.Status406NotAcceptable;
                return;
            }

            var authentication = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            if (!authentication.Succeeded || !authentication.Principal.Identity.IsAuthenticated)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                using Utf8JsonWriter writer = new Utf8JsonWriter(context.Response.BodyWriter);
                writer.WriteStartObject();
                writer.WriteString(errJson, authenticationFailedJson);
                writer.WriteEndObject();
                return;
            }

            var user = authentication.Principal.Identity.Name ?? "*";
            if (!_registrations.TryGetValue(user, out var policy))
            {
                await RefreshPolicies(_options);
                if (!_registrations.TryGetValue(user, out policy))
                {
                    // TODO : refresh policies 1 time
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json";
                    using Utf8JsonWriter writer = new Utf8JsonWriter(context.Response.BodyWriter);
                    writer.WriteStartObject();
                    writer.WriteString(errJson, accessDeniedJson);
                    writer.WriteEndObject();
                    return;
                }
            }

            var readResult = await request.BodyReader.ReadAsync();
            var response = await _receiver.TryStoreToken(readResult.Buffer, policy);
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
            }

            request.BodyReader.AdvanceTo(readResult.Buffer.End);
        }

        private Task RefreshPolicies(EventReceiverOptions _options)
        {
            // TODO
            return Task.CompletedTask;
        }
    }
}
