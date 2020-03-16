using System;
using Uruk.Server;

namespace Microsoft.Extensions.DependencyInjection
{
    internal class EventReceiverBuilder : IEventReceiverBuilder
    {
        public EventReceiverBuilder(IServiceCollection services, string audience)
        {
            Services = services;
            Services.Configure<EventReceiverOptions>(options =>
            {
                options.Audience = audience;
            });
        }

        public IServiceCollection Services { get; }

        public IEventReceiverBuilder Add(EventReceiverRegistration registration)
        {
            if (registration == null)
            {
                throw new ArgumentNullException(nameof(registration));
            }

            Services.Configure<EventReceiverOptions>(options =>
            {
                options.Registrations.Add(registration);
            });

            return this;
        }
    }
}