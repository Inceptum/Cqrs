using System;
using System.Collections.Generic;
using CommonDomain.Persistence;
using Inceptum.Cqrs.EventSourcing;
using NEventStore;
using NEventStore.Dispatcher;
using NEventStore.Persistence.SqlPersistence;

namespace Inceptum.Cqrs.Configuration
{
    internal class NEventStoreDescriptor : EventStoreDescriptor
    {
        private readonly Func<IDispatchCommits, IConnectionFactory, Wireup> m_ConfigureEventStore;
        private IConstructAggregates m_ConstructAggregates;
        private IConnectionFactory m_ConnectionFactory;

        public NEventStoreDescriptor(Func<IDispatchCommits, IConnectionFactory, Wireup> configureEventStore)
        {
            if (configureEventStore == null) throw new ArgumentNullException("configureEventStore");
            m_ConfigureEventStore = configureEventStore;
        }
        
        public override void Create(BoundedContext boundedContext, IDependencyResolver resolver)
        {
            m_ConstructAggregates = resolver.HasService(typeof (IConstructAggregates))
                ? (IConstructAggregates)resolver.GetService(typeof(IConstructAggregates))
                : null;

            m_ConnectionFactory = resolver.HasService(typeof(IConnectionFactory))
                ? (IConnectionFactory)resolver.GetService(typeof(IConnectionFactory))
                : new ConfigurationConnectionFactory(null);

            IStoreEvents eventStore = m_ConfigureEventStore(new CommitDispatcher(boundedContext.EventsPublisher), m_ConnectionFactory).Build();
            EventStoreAdapter=new NEventStoreAdapter(eventStore, m_ConstructAggregates);
        }
    }
}