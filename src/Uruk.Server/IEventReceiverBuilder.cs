using Uruk.Server;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// A builder used to register event transmitter.
    /// </summary>
    public interface IEventReceiverBuilder
    {
        /// <summary>
        /// Adds a <see cref="EventReceiverRegistration"/>.
        /// </summary>
        /// <param name="registration">The <see cref="HealthCheckRegistration"/>.</param>
        IEventReceiverBuilder Add(EventReceiverRegistration registration);

        /// <summary>
        /// Gets the <see cref="IServiceCollection"/>.
        /// </summary>
        IServiceCollection Services { get; }
    }
}