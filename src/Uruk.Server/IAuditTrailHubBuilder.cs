using Uruk.Server;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// A builder used to register event transmitter.
    /// </summary>
    public interface IAuditTrailHubBuilder
    {
        /// <summary>
        /// Adds a <see cref="AuditTrailHubRegistration"/>.
        /// </summary>
        /// <param name="registration">The <see cref="AuditTrailHubRegistration"/>.</param>
        IAuditTrailHubBuilder RegisterClient(AuditTrailHubRegistration registration);

        /// <summary>
        /// Gets the <see cref="IServiceCollection"/>.
        /// </summary>
        IServiceCollection Services { get; }
    }
}