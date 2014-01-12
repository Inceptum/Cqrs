namespace Inceptum.Cqrs.Configuration
{
    public static class LocalBoundedContextRegistrationExtensions
    {
        public static SendingCommandsDescriptor WithLoopback(this ListeningCommandsDescriptor descriptor, string route)
        {
            return descriptor.SendingCommands(descriptor.Types).To(descriptor.BoundedContextName).With(route);
        }

        public static ListeningEventsDescriptor WithLoopback(this PublishingEventsDescriptor descriptor, string route)
        {
            return descriptor.ListeningEvents(descriptor.Types).From(descriptor.BoundedContextName).On(route);
        }

    }
}