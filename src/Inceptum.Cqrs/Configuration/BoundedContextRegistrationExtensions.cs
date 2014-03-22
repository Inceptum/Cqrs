using Inceptum.Cqrs.Configuration.BoundedContext;
using Inceptum.Cqrs.Configuration.Routing;
using Inceptum.Cqrs.InfrastructureCommands;

namespace Inceptum.Cqrs.Configuration
{
    public static class BoundedContextRegistrationExtensions
    {
        public static PublishingCommandsDescriptor<IBoundedContextRegistration> WithLoopback(this ListeningCommandsDescriptor<IBoundedContextRegistration> descriptor, string route = null)
        {
            route = route ?? descriptor.Route;
            IRegistrationWrapper<IBoundedContextRegistration> wrapper = descriptor;
            return descriptor.PublishingCommands(descriptor.Types).To(wrapper.Registration.Name).With(route);
        }

        public static ListeningEventsDescriptor<IBoundedContextRegistration> WithLoopback(this PublishingEventsDescriptor<IBoundedContextRegistration> descriptor, string route = null)
        {
            route = route ?? descriptor.Route;
            IRegistrationWrapper<IBoundedContextRegistration> wrapper = descriptor;
            return descriptor.ListeningEvents(descriptor.Types).From(wrapper.Registration.Name).On(route);
        }

        public static IListeningRouteDescriptor<ListeningCommandsDescriptor<IBoundedContextRegistration>> ListeningInfrastructureCommands(this IBoundedContextRegistration registration)
        {
            return registration.ListeningCommands(typeof(ReplayEventsCommand));
        }

        public static IPublishingCommandsDescriptor<IBoundedContextRegistration> PublishingInfrastructureCommands(this IBoundedContextRegistration registration)
        {
            return registration.PublishingCommands(typeof(ReplayEventsCommand));
        }


        public static IListeningRouteDescriptor<ListeningCommandsDescriptor<IBoundedContextRegistration>> ListeningInfrastructureCommands(this IRegistrationWrapper<IBoundedContextRegistration> registration)
        {
            return registration.ListeningCommands(typeof(ReplayEventsCommand));
        }

        public static IPublishingCommandsDescriptor<IBoundedContextRegistration> PublishingInfrastructureCommands<TRegistration>(this IRegistrationWrapper<IBoundedContextRegistration> registration)
        {
            return registration.PublishingCommands(typeof(ReplayEventsCommand));
        }

    }
}