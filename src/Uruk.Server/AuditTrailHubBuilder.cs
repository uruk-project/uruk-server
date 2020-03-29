using System;
using Uruk.Server;

namespace Microsoft.Extensions.DependencyInjection
{
    internal class AuditTrailHubBuilder : IAuditTrailHubBuilder
    {
        public AuditTrailHubBuilder(IServiceCollection services, string audience)
        {
            Services = services;
            Services.Configure<AuditTrailHubOptions>(options =>
            {
                options.Audience = audience;
            });
        }

        public IServiceCollection Services { get; }

        public IAuditTrailHubBuilder Add(AuditTrailHubRegistration registration)
        {
            if (registration == null)
            {
                throw new ArgumentNullException(nameof(registration));
            }

            Services.Configure<AuditTrailHubOptions>(options =>
            {
                options.Registrations.Add(registration);
            });

            return this;
        }
    }
}