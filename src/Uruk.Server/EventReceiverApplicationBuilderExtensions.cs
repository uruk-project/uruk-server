using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Uruk.Server
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

        private static void UseEventReceiverCore(IApplicationBuilder app, PathString path, object[] args)
        {
            if (app.ApplicationServices.GetService(typeof(IEventReceiverService)) == null)
            {
                throw new InvalidOperationException($"Unable to find the required services. Please add all the required services by calling 'nameof(IServiceCollection).nameof(EventReceiverServiceCollectionExtensions.AddEventReceiver)' inside the call to 'ConfigureServices(...)' in the application startup code."); 
            }

            app.Map(path, app => app.UseAuthentication().UseMiddleware<EventReceiverMiddleware>(args));
        }
    }
}