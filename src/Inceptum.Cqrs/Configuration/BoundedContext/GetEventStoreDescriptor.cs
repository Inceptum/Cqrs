using System;
using System.Collections.Generic;
using CommonDomain.Persistence;
using EventStore.ClientAPI;

namespace Inceptum.Cqrs.Configuration.BoundedContext
{
    internal class GetEventStoreDescriptor : IDescriptor<Context>
    {
        private readonly IEventStoreConnection m_EventStoreConnection;

        public GetEventStoreDescriptor(IEventStoreConnection eventStoreConnection)
        {
            m_EventStoreConnection = eventStoreConnection;
        }

        public IEnumerable<Type> GetDependencies()
        {
            return new Type[0];
        }

        public void Create(Cqrs.Context context, IDependencyResolver resolver)
        {
            var aggregateConstructor = resolver.HasService(typeof (IConstructAggregates))
                                           ? (IConstructAggregates) resolver.GetService(typeof (IConstructAggregates))
                                           : null;
            context.EventStore = new GetEventStoreAdapter(m_EventStoreConnection, context.EventsPublisher, aggregateConstructor);
        }

        public void Process(Cqrs.Context context, CqrsEngine cqrsEngine)
        {

        }
    }
}