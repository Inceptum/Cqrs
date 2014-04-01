using System;
using System.Collections.Generic;
using CommonDomain.Persistence;
using EventStore.ClientAPI;

namespace Inceptum.Cqrs.Configuration.BoundedContext
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