using System;
using EventStore.ClientAPI;
using NEventStore;
using NEventStore.Dispatcher;

namespace Inceptum.Cqrs.Configuration
{
    public interface IBoundedContextRegistration : IRegistration, IHideObjectMembers
    {
        string BoundedContextName { get; }
    

        IBoundedContextRegistration FailedCommandRetryDelay(long delay);
        PublishingCommandsDescriptor PublishingCommands(params Type[] commandsTypes);
        ListeningEventsDescriptor ListeningEvents(params Type[] type);
        IListeningRouteDescriptor<ListeningCommandsDescriptor> ListeningCommands(params Type[] type);
        IPublishingRouteDescriptor<PublishingEventsDescriptor> PublishingEvents(params Type[] type);
        ProcessingOptionsDescriptor ProcessingOptions(string route);

        IBoundedContextRegistration WithProjection(object projection, string fromBoundContext);
        IBoundedContextRegistration WithProjection(Type projection, string fromBoundContext);
        IBoundedContextRegistration WithProjection<TListener>(string fromBoundContext);

        IBoundedContextRegistration WithCommandsHandler(object handler);
        IBoundedContextRegistration WithCommandsHandler<T>();
        IBoundedContextRegistration WithCommandsHandlers(params Type[] handlers);
        IBoundedContextRegistration WithCommandsHandler(Type handler);


        IBoundedContextRegistration WithSaga(object saga, params string[] listenedBoundContext);
        IBoundedContextRegistration WithSaga(Type saga, params string[] listenedBoundContext);
        IBoundedContextRegistration WithSaga<T>(params string[] listenedBoundContext);

        IBoundedContextRegistration WithEventStore(Func<IDispatchCommits, Wireup> configureEventStore);
        IBoundedContextRegistration WithEventStore(IEventStoreConnection eventStoreConnection);


        IBoundedContextRegistration WithProcess(object process);
        IBoundedContextRegistration WithProcess(Type process);
        IBoundedContextRegistration WithProcess<TProcess>() where TProcess : IProcess;
    }
}