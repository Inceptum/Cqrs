using System;
using Inceptum.Cqrs.Configuration.Routing;
using NEventStore;
using NEventStore.Dispatcher;
using NEventStore.Persistence.SqlPersistence;

namespace Inceptum.Cqrs.Configuration.BoundedContext
{
    public interface IBoundedContextRegistration : IRegistration, IHideObjectMembers
    {
        string Name { get; }
        bool HasEventStore { get; }
 
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


        IBoundedContextRegistration WithProjection(object projection, string fromBoundContext, int batchSize = 0, int applyTimeoutInSeconds = 0);
        IBoundedContextRegistration WithProjection(Type projection, string fromBoundContext, int batchSize = 0, int applyTimeoutInSeconds = 0);
        IBoundedContextRegistration WithProjection<TListener>(string fromBoundContext);


        IBoundedContextRegistration WithEventStore<T>() where T : IEventStoreAdapter;
        IBoundedContextRegistration WithEventStore(IEventStoreAdapter eventStoreAdapter);
        IBoundedContextRegistration WithEventStore(Func<Context, IDependencyResolver, IEventStoreAdapter> eventStoreAdapterFactory);
        [Obsolete] IBoundedContextRegistration WithNEventStore(Func<IDispatchCommits, Wireup> configureEventStore);
        [Obsolete] IBoundedContextRegistration WithNEventStore(Func<IDispatchCommits, IConnectionFactory, Wireup> configureEventStore);
        IBoundedContextRegistration WithNEventStore(Func<Wireup> configureEventStore);
        IBoundedContextRegistration WithNEventStore(Func<IConnectionFactory, Wireup> configureEventStore);

        IBoundedContextRegistration WithProcess(object process);
        IBoundedContextRegistration WithProcess(Type process);
        IBoundedContextRegistration WithProcess<TProcess>() where TProcess : IProcess;

 

    }
}