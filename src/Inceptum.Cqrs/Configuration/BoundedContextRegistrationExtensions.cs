using Inceptum.Cqrs.InfrastructureCommands;

namespace Inceptum.Cqrs.Configuration
{
    public static class BoundedContextRegistrationExtensions
    {
        public static PublishingCommandsDescriptor WithLoopback(this ListeningCommandsDescriptor descriptor, string route=null)
        {
            route = route ?? descriptor.Route;
            return descriptor.PublishingCommands(descriptor.Types).To(descriptor.BoundedContextName).With(route);
        }

        public static ListeningEventsDescriptor WithLoopback(this PublishingEventsDescriptor descriptor, string route=null)
        {
            route = route ?? descriptor.Route;
            return descriptor.ListeningEvents(descriptor.Types).From(descriptor.BoundedContextName).On(route);
        }

        public static  IListeningRouteDescriptor<ListeningCommandsDescriptor> ListeningInfrastructureCommands(this IBoundedContextRegistration registration)
        {
            return registration.ListeningCommands(typeof(ReplayEventsCommand));
        }

        public static IPublishingCommandsDescriptor PublishingInfrastructureCommands(this IBoundedContextRegistration registration)
        {
            return registration.PublishingCommands(typeof(ReplayEventsCommand));
        }
    }
}