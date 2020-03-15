using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace UrukServer
{
    /// <summary>
    /// <see cref="IApplicationBuilder"/> extension methods for the <see cref="EventReceiverMiddleware"/>.
    /// </summary>
    public static class EventReceiverApplicationBuilderExtensions
    {
        /// <summary>
        /// Adds a middleware that accepts security event tokens.
        /// </summary>
        /// <param name="app">The <see cref="IApplicationBuilder"/>.</param>
        /// <param name="path">The path on which to accepts security event tokens.</param>
        /// <returns>A reference to the <paramref name="app"/> after the operation has completed.</returns>
        public static IApplicationBuilder UseEventReceiver(this IApplicationBuilder app, PathString path)
        {
            if (app == null)
            {
                throw new ArgumentNullException(nameof(app));
            }

            UseEventReceiverCore(app, path, Array.Empty<object>());
            return app;
        }

        /// <summary>
        /// Adds a middleware that accepts security event tokens.
        /// </summary>
        /// <param name="app">The <see cref="IApplicationBuilder"/>.</param>
        /// <param name="path">The path on which to accept security event tokens.</param>
        /// <param name="options">A <see cref="EventReceiverOptions"/> used to configure the middleware.</param>
        /// <returns>A reference to the <paramref name="app"/> after the operation has completed.</returns>
        public static IApplicationBuilder UseEventReceiver(this IApplicationBuilder app, PathString path, EventReceiverOptions options)
        {
            if (app == null)
            {
                throw new ArgumentNullException(nameof(app));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            UseEventReceiverCore(app, path, new[] { Options.Create(options), });
            return app;
        }

        /// <summary>
        /// Adds a middleware that accepts security event tokens.
        /// </summary>
        /// <param name="app">The <see cref="IApplicationBuilder"/>.</param>
        /// <param name="path">The path on which to accept security event tokens.</param>
        /// <param name="options">A <see cref="EventReceiverOptions"/> used to configure the middleware.</param>
        /// <returns>A reference to the <paramref name="app"/> after the operation has completed.</returns>
        public static IApplicationBuilder UseEventReceiver(this IApplicationBuilder app, PathString path, string audience)
        {
            if (app == null)
            {
                throw new ArgumentNullException(nameof(app));
            }

            if (audience == null)
            {
                throw new ArgumentNullException(nameof(audience));
            }

            var options = new EventReceiverOptions();
            options.Audience = audience;
            UseEventReceiverCore(app, path, new[] { Options.Create(options), });
            return app;
        }

        private static void UseEventReceiverCore(IApplicationBuilder app, PathString path, object[] args)
        {
            if (app.ApplicationServices.GetService(typeof(IEventReceiverService)) == null)
            {
                throw new InvalidOperationException(); 
                // Resources.FormatUnableToFindServices(
                //    nameof(IServiceCollection),
                //    nameof(HealthCheckServiceCollectionExtensions.AddHealthChecks),
                //    "ConfigureServices(...)"));
            }

            app.Map(path, app => app.UseMiddleware<EventReceiverMiddleware>(args));
        }
    }
}