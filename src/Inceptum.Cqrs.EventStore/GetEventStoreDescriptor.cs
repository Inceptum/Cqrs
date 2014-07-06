using CommonDomain.Persistence;
using EventStore.ClientAPI;
using Inceptum.Cqrs.Configuration;
using Inceptum.Cqrs.Configuration.BoundedContext;

namespace Inceptum.Cqrs.EventStore
{
    internal class GetEventStoreDescriptor : EventStoreDescriptorBase
    {
        private readonly IEventStoreConnection m_EventStoreConnection;

        public GetEventStoreDescriptor(IEventStoreConnection eventStoreConnection)
        {
            m_EventStoreConnection = eventStoreConnection;
        }

        protected override IEventStoreAdapter CreateEventStore(Context boundedContext, IDependencyResolver resolver)
        {
            var aggregateConstructor = resolver.HasService(typeof(IConstructAggregates))
                                        ? (IConstructAggregates)resolver.GetService(typeof(IConstructAggregates))
                                        : null;
            return new GetEventStoreAdapter(m_EventStoreConnection, boundedContext.EventsPublisher, aggregateConstructor);
        }
    }
}