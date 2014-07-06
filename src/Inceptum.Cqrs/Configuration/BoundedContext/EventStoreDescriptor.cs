using System;
using System.Collections.Generic;
using CommonDomain.Persistence;
using Inceptum.Cqrs.EventSourcing;
using NEventStore;
using NEventStore.Dispatcher;
using NEventStore.Persistence.SqlPersistence;

namespace Inceptum.Cqrs.Configuration.BoundedContext
{
    internal class EventStoreDescriptor<T> : EventStoreDescriptorBase where T : IEventStoreAdapter
    {
        public override IEnumerable<Type> GetDependencies()
        {
            return new Type[] { typeof(T) };
        }


        protected override IEventStoreAdapter CreateEventStore(Context boundedContext, IDependencyResolver resolver)
        {
            return (T)resolver.GetService(typeof(T));
        }
    }

    public abstract class EventStoreDescriptorBase : IDescriptor<Context>
    {


        public virtual IEnumerable<Type> GetDependencies()
        {
            return new Type[0];
        }

        public void Create(Context boundedContext, IDependencyResolver resolver)
        {
            boundedContext.EventStore = CreateEventStore(boundedContext,resolver);
        }

        public void Process(Context boundedContext, CqrsEngine cqrsEngine)
        {
            
        }

        protected abstract IEventStoreAdapter CreateEventStore(Context boundedContext, IDependencyResolver resolver);

    }
    class EventStoreDescriptor : EventStoreDescriptorBase
    {
        private IEventStoreAdapter m_EventStoreAdapter;
        private Func<Context, IDependencyResolver, IEventStoreAdapter> m_EventStoreAdapterFactory;

        public EventStoreDescriptor(IEventStoreAdapter eventStoreAdapter)
        {
            m_EventStoreAdapter = eventStoreAdapter;
        }

        public EventStoreDescriptor(Func<Context, IDependencyResolver, IEventStoreAdapter> eventStoreAdapterFactory)
        {
            m_EventStoreAdapterFactory = eventStoreAdapterFactory;
        }

        protected override IEventStoreAdapter CreateEventStore(Context boundedContext, IDependencyResolver resolver)
        {
            if (m_EventStoreAdapterFactory != null)
                m_EventStoreAdapter = m_EventStoreAdapterFactory(boundedContext, resolver);
            return m_EventStoreAdapter;
        }
    }


    internal class NEventStoreDescriptor : EventStoreDescriptorBase
    {
        private readonly Func<IDispatchCommits, IConnectionFactory, Wireup> m_ConfigureEventStore;
        private IConstructAggregates m_ConstructAggregates;
        private IConnectionFactory m_ConnectionFactory;

        public NEventStoreDescriptor(Func<IDispatchCommits, IConnectionFactory, Wireup> configureEventStore)
        {
            if (configureEventStore == null) throw new ArgumentNullException("configureEventStore");
            m_ConfigureEventStore = configureEventStore;
        }

        protected override IEventStoreAdapter CreateEventStore(Context boundedContext, IDependencyResolver resolver)
        {
            m_ConstructAggregates = resolver.HasService(typeof(IConstructAggregates))
                ? (IConstructAggregates)resolver.GetService(typeof(IConstructAggregates))
                : null;

            m_ConnectionFactory = resolver.HasService(typeof(IConnectionFactory))
                ? (IConnectionFactory)resolver.GetService(typeof(IConnectionFactory))
                : new ConfigurationConnectionFactory(null);

            IStoreEvents eventStore = m_ConfigureEventStore(new CommitDispatcher(boundedContext.EventsPublisher), m_ConnectionFactory).Build();
            return new NEventStoreAdapter(eventStore, m_ConstructAggregates);
        }
    }
}