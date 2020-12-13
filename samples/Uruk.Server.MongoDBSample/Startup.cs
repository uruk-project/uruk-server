using System.Security.Cryptography;
using JsonWebToken;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Uruk.Server.MongoDB;

namespace Uruk.Server.MongoDBSample
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            var ecdsa = ECDsa.Create();
            ecdsa.GenerateKey(ECCurve.NamedCurves.nistP256);
            services.AddAuditTrailHub("636C69656E745F6964")
                .RegisterClient(new AuditTrailHubRegistration("m2m", SignatureAlgorithm.HS256, SymmetricJwk.FromBase64Url("R9MyWaEoyiMYViVWo8Fk4TUGWiSoaW6U1nOqXri8ZXU")))
                .AddMongoDBStorage("mongodb://localhost")
                .AddMongoDBMerkleTree(SupportedHashAlgorithm.Sha256, ecdsa.ExportParameters(true));

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                  .AddJwtBearer(o =>
                  {
                      o.Authority = Configuration["authentication:authority"];
                      o.Audience = Configuration["authentication:audience"];
                  });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseAuditTrailHub("/events");
        }
    }
}
