using System;

namespace Inceptum.Cqrs.Configuration
{
    public interface IBoundedContextRegistration : IRegistration, IHideObjectMembers
    {
        SendingCommandsDescriptor SendingCommands(params Type[] commandsTypes);
        ListeningEventsDescriptor ListeningEvents(params Type[] type);
        IListeningRouteDescriptor<ListeningCommandsDescriptor> ListeningCommands(params Type[] type);
        IPublishingRouteDescriptor<PublishingEventsDescriptor> PublishingEvents(params Type[] type);
        ProcessingOptionsDescriptor ProcessingOptions(string route);
    }
}