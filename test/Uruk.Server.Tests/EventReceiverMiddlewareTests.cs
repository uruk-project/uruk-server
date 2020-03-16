using System;
using System.Buffers;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using JsonWebToken;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Uruk.Server.Tests
{
    public class EventReceiverMiddlewareTests
    {
        public EventReceiverMiddlewareTests()
        {
            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
        }

        [Fact]
        public void ThrowFriendlyErrorWhenServicesNotRegistered()
        {
            var builder = new WebHostBuilder()
                .Configure(app => app.UseEventReceiver("/events"))
                .ConfigureServices(services =>
                {
                    services.AddAuthentication();
                }); ;

            var ex = Assert.Throws<InvalidOperationException>(() => new TestServer(builder));

            Assert.Equal(
                "Unable to find the required services. Please add all the required services by calling " +
                "'nameof(IServiceCollection).nameof(EventReceiverServiceCollectionExtensions.AddEventReceiver)' " +
                "inside the call to 'ConfigureServices(...)' in the application startup code.",
                ex.Message);
        }

        [Fact]
        public async Task ReturnsNotFoundWhenInvalidPath()
        {
            var builder = new WebHostBuilder()
                .Configure(app => app.UseEventReceiver("/events"))
                .ConfigureServices(services =>
                {
                    services.AddAuthentication();
                    services.AddEventReceiver("uruk");
                });
            var server = new TestServer(builder);

            var response = await server.CreateClient().PostAsync("/not-events", new StringContent(CreateSecurityEventToken()));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task ReturnsNotFoundWhenInvalidHttpMethod()
        {
            var builder = new WebHostBuilder()
                .Configure(app => app.UseEventReceiver("/events"))
                .ConfigureServices(services =>
                {
                    services.AddAuthentication();
                    services.AddEventReceiver("uruk");
                });
            var server = new TestServer(builder);

            var response = await server.CreateClient().GetAsync("/events");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task ReturnsUnsupportedMediaTypeWhenInvalidMediaType()
        {
            var builder = new WebHostBuilder()
                .Configure(app => app.UseEventReceiver("/events"))
                .ConfigureServices(services =>
                {
                    services.AddAuthentication();
                    services.AddEventReceiver("uruk");
                });
            var server = new TestServer(builder);

            var response = await server.CreateClient().PostAsync("/events", new StringContent(CreateSecurityEventToken()));

            Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        }

        [Fact]
        public async Task ReturnsUnsupportedMediaTypeWhenNoMediaType()
        {
            var builder = new WebHostBuilder()
                .Configure(app => app.UseEventReceiver("/events"))
                .ConfigureServices(services =>
                {
                    services.AddAuthentication();
                    services.AddEventReceiver("uruk");
                });
            var server = new TestServer(builder);
            var content = new StringContent(CreateSecurityEventToken());
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            var response = await server.CreateClient().PostAsync("/events", content);

            Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        }

        [Fact]
        public async Task ReturnsNotAcceptableWhenNoAccept()
        {
            var builder = new WebHostBuilder()
                .Configure(app => app.UseEventReceiver("/events"))
                .ConfigureServices(services =>
                {
                    services.AddAuthentication();
                    services.AddEventReceiver("uruk");
                });
            var server = new TestServer(builder);
            var content = new StringContent(CreateSecurityEventToken());
            content.Headers.ContentType = new MediaTypeHeaderValue("application/secevent+jwt");
            var response = await server.CreateClient().PostAsync("/events", content);

            Assert.Equal(HttpStatusCode.NotAcceptable, response.StatusCode);
        }

        [Fact]
        public async Task ReturnsNotAcceptableWhenNoInvalidAccept()
        {
            var builder = new WebHostBuilder()
                .Configure(app => app.UseEventReceiver("/events"))
                .ConfigureServices(services =>
                {
                    services.AddAuthentication();
                    services.AddEventReceiver("uruk");
                });
            var server = new TestServer(builder);
            var message = new HttpRequestMessage(HttpMethod.Post, "/events");
            message.Headers.Accept.ParseAdd("application/xml");
            message.Content = new StringContent(CreateSecurityEventToken());
            message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/secevent+jwt");
            var response = await server.CreateClient().SendAsync(message);

            Assert.Equal(HttpStatusCode.NotAcceptable, response.StatusCode);
        }

        [Fact]
        public async Task ReturnsUnauthorizedWhenNotValidAuthentication()
        {
            var builder = new WebHostBuilder()
                .Configure(app =>
                {
                    app.UseEventReceiver("/events");
                })
                .ConfigureServices(services =>
                {
                    services.AddEventReceiver("uruk");
                    services.AddAuthentication()
                        .AddJwtBearer(o =>
                        {
                            o.TokenValidationParameters = new TokenValidationParameters()
                            {
                                ValidIssuer = "issuer.example.com",
                                ValidAudience = "audience.example.com",
                                IssuerSigningKey = GetKey(),
                                NameClaimType = "sub"
                            };
                        });
                });
            var server = new TestServer(builder);
            var client = server.CreateClient();

            var message = new HttpRequestMessage(HttpMethod.Post, "/events");
            message.Headers.Accept.ParseAdd("application/json");
            message.Content = new StringContent(CreateSecurityEventToken());
            message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/secevent+jwt");
            var response = await server.CreateClient().SendAsync(message);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            Assert.Equal("application/json", response.Content.Headers.ContentType.ToString());
            Assert.Equal("{\"err\":\"authentication_failed\"}", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task ReturnsUnauthorizedWhenNotAuthorizedUser()
        {
            var builder = new WebHostBuilder()
                .Configure(app =>
                {
                    app.UseEventReceiver("/events");
                })
                .ConfigureServices(services =>
                {
                    services.AddEventReceiver("uruk")
                        .Add(new EventReceiverRegistration("bad_user", SignatureAlgorithm.HmacSha256, GetJwk()));
                    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                        .AddJwtBearer(o =>
                        {
                            o.TokenValidationParameters = new TokenValidationParameters()
                            {
                                ValidIssuer = "issuer.example.com",
                                ValidAudience = "audience.example.com",
                                IssuerSigningKey = GetKey(),
                                NameClaimType = "sub"
                            };
                        });
                });
            var server = new TestServer(builder);
            var client = server.CreateClient();

            var message = new HttpRequestMessage(HttpMethod.Post, "/events");
            message.Headers.Accept.ParseAdd("application/json");
            message.Content = new StringContent(CreateSecurityEventToken());
            message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/secevent+jwt");
            message.Headers.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, CreateBearerToken());
            var response = await client.SendAsync(message);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

            Assert.Equal("application/json", response.Content.Headers.ContentType.ToString());
            Assert.Equal("{\"err\":\"access_denied\"}", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task ReturnsAccepted()
        {
            var builder = new WebHostBuilder()
                .Configure(app =>
                {
                    app.UseEventReceiver("/events");
                })
                .ConfigureServices(services =>
                {
                    services.AddEventReceiver("uruk")
                        .Add(new EventReceiverRegistration("bad_user", SignatureAlgorithm.HmacSha256, GetJwk()))
                        .Add(new EventReceiverRegistration("Bob", SignatureAlgorithm.HmacSha256, GetJwk()));
                    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                        .AddJwtBearer(o =>
                        {
                            o.TokenValidationParameters = new TokenValidationParameters()
                            {
                                ValidIssuer = "issuer.example.com",
                                ValidAudience = "audience.example.com",
                                IssuerSigningKey = GetKey(),
                                NameClaimType = "sub"
                            };
                        });
                });
            var server = new TestServer(builder);
            var client = server.CreateClient();

            var message = new HttpRequestMessage(HttpMethod.Post, "/events");
            message.Headers.Accept.ParseAdd("application/json");
            message.Content = new StringContent(CreateSecurityEventToken());
            message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/secevent+jwt");
            message.Headers.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, CreateBearerToken());
            var response = await client.SendAsync(message);
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            Assert.Null(response.Content.Headers.ContentType);
            Assert.Equal(0, response.Content.Headers.ContentLength);
        }

        [Fact]
        public async Task ReturnsError()
        {
            var builder = new WebHostBuilder()
                .Configure(app =>
                {
                    app.UseEventReceiver("/events");
                })
                .ConfigureServices(services =>
                {
                    services.AddEventReceiver("uruk")
                        .Add(new EventReceiverRegistration("bad_user", SignatureAlgorithm.HmacSha256, GetJwk()))
                        .Add(new EventReceiverRegistration("Bob", SignatureAlgorithm.HmacSha256, GetJwk()));
                    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                        .AddJwtBearer(o =>
                        {
                            o.TokenValidationParameters = new TokenValidationParameters()
                            {
                                ValidIssuer = "issuer.example.com",
                                ValidAudience = "audience.example.com",
                                IssuerSigningKey = GetKey(),
                                NameClaimType = "sub"
                            };
                        });
                    services.AddTransient<IEventReceiverService, ErrorEventReceiverService>();
                });
            var server = new TestServer(builder);
            var client = server.CreateClient();

            var message = new HttpRequestMessage(HttpMethod.Post, "/events");
            message.Headers.Accept.ParseAdd("application/json");
            message.Content = new StringContent(CreateSecurityEventToken());
            message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/secevent+jwt");
            message.Headers.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, CreateBearerToken());
            var response = await client.SendAsync(message);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            Assert.Equal("application/json", response.Content.Headers.ContentType.ToString());
            Assert.Equal("{\"err\":\"test_error\",\"description\":\"Error description\"}", await response.Content.ReadAsStringAsync());
        }

        private static SecurityKey GetKey()
            => new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('a', 128)));

        private static Jwk GetJwk()
            => new SymmetricJwk(Encoding.UTF8.GetBytes(new string('a', 128)));

        private static string CreateBearerToken()
        {
            var key = GetKey();
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim("sub", "Bob")
            };

            var token = new JwtSecurityToken(
                issuer: "issuer.example.com",
                audience: "audience.example.com",
                claims: claims,
                expires: DateTime.Now.AddMinutes(30),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static string CreateSecurityEventToken()
        {
            var key = GetJwk();

            var token = new SecurityEventTokenDescriptor
            {
                Issuer = "Bob",
                Audience = "uruk",
                Algorithm = SignatureAlgorithm.HmacSha256,
                IssuedAt = DateTime.UtcNow,
                JwtId = Guid.NewGuid().ToString("N"),
                SigningKey = key
            };

            token.AddEvent("test", new JwtObject());

            return new JwtWriter().WriteTokenString(token);
        }

        private class ErrorEventReceiverService : IEventReceiverService
        {
            public Task<TokenResponse> TryStoreToken(ReadOnlySequence<byte> buffer, TokenValidationPolicy policy)
            {
                return Task.FromResult(new TokenResponse { Succeeded = false, Error = JsonEncodedText.Encode("test_error"), Description = "Error description" });
            }
        }
    }
}
