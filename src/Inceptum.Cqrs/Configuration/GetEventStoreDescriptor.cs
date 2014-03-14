using System;
using System.Collections.Generic;
using System.Reflection;
using CommonDomain;
using CommonDomain.Core;
using CommonDomain.Persistence;
using CommonDomain.Persistence.EventStore;
using EventStore;
using EventStore.ClientAPI;
using NEventStore.Dispatcher;

namespace Inceptum.Cqrs.Configuration
{
    internal class GetEventStoreDescriptor : EventStoreDescriptor
    {
        private readonly IEventStoreConnection m_EventStoreConnection;

        public GetEventStoreDescriptor(IEventStoreConnection eventStoreConnection)
        {
            m_EventStoreConnection = eventStoreConnection;
        }

        public override void Create(BoundedContext boundedContext, IDependencyResolver resolver)
        {
            var aggregateConstructor = resolver.HasService(typeof (IConstructAggregates))
                                           ? (IConstructAggregates) resolver.GetService(typeof (IConstructAggregates))
                                           : null;
            EventStoreAdapter = new GetEventStoreAdapter(m_EventStoreConnection, boundedContext.EventsPublisher, aggregateConstructor);
        }
    }
}