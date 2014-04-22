using System;
using Inceptum.Cqrs.Configuration.BoundedContext;

namespace Inceptum.Cqrs.Configuration.Routing
{
    public class DefaultRoutingRegistration : RegistrationBase<IDefaultRoutingRegistration,IRouteMap>, IDefaultRoutingRegistration
    {
        protected override IRouteMap GetSubject(CqrsEngine cqrsEngine)
        {
            return cqrsEngine.DefaultRouteMap;
        }

        public IPublishingCommandsDescriptor<IDefaultRoutingRegistration> PublishingCommands(params Type[] commandsTypes)
        {
            return AddDescriptor(new PublishingCommandsDescriptor<IDefaultRoutingRegistration>(this, commandsTypes));
        }
    }
}