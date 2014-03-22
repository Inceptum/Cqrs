using System;
using Inceptum.Cqrs.Configuration.Routing;

namespace Inceptum.Cqrs.Configuration.Saga
{
    public interface ISagaRegistration : IRegistration, IHideObjectMembers
    {
        string Name { get; }
        IListeningEventsDescriptor<ISagaRegistration> ListeningEvents(params Type[] types);
        IPublishingCommandsDescriptor<ISagaRegistration> PublishingCommands(params Type[] commandsTypes);
        ProcessingOptionsDescriptor<ISagaRegistration> ProcessingOptions(string route);
    }
}