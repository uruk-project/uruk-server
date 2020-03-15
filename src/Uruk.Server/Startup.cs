using JsonWebToken;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Uruk.Server
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
            services.AddEventReceiver("uruk")
                .Add(new EventReceiverRegistration("*", SignatureAlgorithm.HmacSha256, new SymmetricJwk("R9MyWaEoyiMYViVWo8Fk4TUGWiSoaW6U1nOqXri8ZXU")));

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                  .AddJwtBearer(o =>
                  {
                      o.Authority = Configuration["authentication:authority"];
                      o.Audience = Configuration["authentication:audience"];
                  });

            services.AddSingleton<IEventSink, InMemoryEventSink>();
            services.AddTransient<IEventReceiverService, EventReceiverService>();
            services.AddHostedService<EventSinkBackgroundService>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseAuthentication();
            app.UseEventReceiver("/events");
        }
    }
}