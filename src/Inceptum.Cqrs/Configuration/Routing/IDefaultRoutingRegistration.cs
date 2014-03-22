using System;

namespace Inceptum.Cqrs.Configuration.Routing
{
    public interface IDefaultRoutingRegistration : IRegistration, IHideObjectMembers
    {
        IPublishingCommandsDescriptor<IDefaultRoutingRegistration> PublishingCommands(params Type[] commandsTypes);
    }
}