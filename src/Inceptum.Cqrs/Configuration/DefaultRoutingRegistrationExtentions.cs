using Inceptum.Cqrs.Configuration.Routing;
using Inceptum.Cqrs.InfrastructureCommands;

namespace Inceptum.Cqrs.Configuration
{
	public static class DefaultRoutingRegistrationExtentions
	{
		public static IPublishingCommandsDescriptor<IDefaultRoutingRegistration> PublishingInfrastructureCommands(this IDefaultRoutingRegistration registration)
		{
			return registration.PublishingCommands(typeof(ReplayEventsCommand));
		}

		public static IPublishingCommandsDescriptor<IDefaultRoutingRegistration> PublishingInfrastructureCommands(this IRegistrationWrapper<IDefaultRoutingRegistration> registration)
		{
			return registration.PublishingCommands(typeof(ReplayEventsCommand));
		}
	}
}