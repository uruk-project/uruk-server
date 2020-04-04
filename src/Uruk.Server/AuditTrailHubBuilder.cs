using System;
using Uruk.Server;

namespace Microsoft.Extensions.DependencyInjection
{
    internal class AuditTrailHubBuilder : IAuditTrailHubBuilder
    {
        public AuditTrailHubBuilder(IServiceCollection services)
        {
            Services = services;
        }

        public IServiceCollection Services { get; }

        public IAuditTrailHubBuilder RegisterClient(AuditTrailHubRegistration registration)
        {
            if (registration == null)
            {
                throw new ArgumentNullException(nameof(registration));
            }

            Services.Configure<AuditTrailHubOptions>(options =>
            {
                options.Registry.Add(registration);
            });

            return this;
        }
    }

    public static class AuditTrailHubBuilderExtensions
    {
        public static IAuditTrailHubBuilder Add(this IAuditTrailHubBuilder builder, AuditTrailHubRegistration registration)
        {
            if (registration == null)
            {
                throw new ArgumentNullException(nameof(registration));
            }

            builder.Services.Configure<AuditTrailHubOptions>(options =>
            {
                options.Registry.Add(registration);
            });

            return builder;
        }
    }
}