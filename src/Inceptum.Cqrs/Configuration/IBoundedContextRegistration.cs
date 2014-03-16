using System;
using EventStore.ClientAPI;
using NEventStore;
using NEventStore.Dispatcher;

namespace Inceptum.Cqrs.Configuration
{

    public class Saga 
    {
  /*      public static ISagaRegistration Named(string name)
        {
            
        }*/
    }

/*


    public interface ISagaRegistration : IRegistration, IHideObjectMembers
    {
        string Name { get; }
        ListeningEventsDescriptor ListeningEvents(params Type[] type);
        PublishingCommandsDescriptor PublishingCommands(params Type[] commandsTypes);

        ProcessingOptionsDescriptor ProcessingOptions(string route);
    }
*/


    public interface IBoundedContextRegistration : IRegistration, IHideObjectMembers
    {
        string BoundedContextName { get; }
        long FailedCommandRetryDelayInternal { get; set; }

        IBoundedContextRegistration FailedCommandRetryDelay(long delay);
        IPublishingCommandsDescriptor<IBoundedContextRegistration> PublishingCommands(params Type[] commandsTypes);

        IListeningEventsDescriptor<IBoundedContextRegistration> ListeningEvents(params Type[] type);

        IListeningRouteDescriptor<ListeningCommandsDescriptor<IBoundedContextRegistration>> ListeningCommands(params Type[] type);
        IPublishingRouteDescriptor<PublishingEventsDescriptor<IBoundedContextRegistration>> PublishingEvents(params Type[] type);


        ProcessingOptionsDescriptor<IBoundedContextRegistration> ProcessingOptions(string route);

        IBoundedContextRegistration WithCommandsHandler(object handler);
        IBoundedContextRegistration WithCommandsHandler<T>();
        IBoundedContextRegistration WithCommandsHandlers(params Type[] handlers);
        IBoundedContextRegistration WithCommandsHandler(Type handler);


        IBoundedContextRegistration WithProjection(object projection, string fromBoundContext);
        IBoundedContextRegistration WithProjection(Type projection, string fromBoundContext);
        IBoundedContextRegistration WithProjection<TListener>(string fromBoundContext);


        IBoundedContextRegistration WithEventStore(Func<IDispatchCommits, Wireup> configureEventStore);
        IBoundedContextRegistration WithEventStore(IEventStoreConnection eventStoreConnection);


        IBoundedContextRegistration WithProcess(object process);
        IBoundedContextRegistration WithProcess(Type process);
        IBoundedContextRegistration WithProcess<TProcess>() where TProcess : IProcess;


        IBoundedContextRegistration WithSaga(object saga, params string[] listenedBoundContext);
        IBoundedContextRegistration WithSaga(Type saga, params string[] listenedBoundContext);
        IBoundedContextRegistration WithSaga<T>(params string[] listenedBoundContext);

    }
}