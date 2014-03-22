using System;
using System.Collections.Generic;
using CommonDomain.Persistence;
using Inceptum.Cqrs.EventSourcing;
using NEventStore;
using NEventStore.Dispatcher;

namespace Inceptum.Cqrs.Configuration.BoundedContext
{
    internal class EventStoreDescriptor : IDescriptor<Context>
    {
        private readonly Func<IDispatchCommits, Wireup> m_ConfigureEventStore;

        public EventStoreDescriptor(Func<IDispatchCommits, Wireup> configureEventStore)
        {
            if (configureEventStore == null) throw new ArgumentNullException("configureEventStore");
            m_ConfigureEventStore = configureEventStore;
        }

        public IEnumerable<Type> GetDependencies()
        {
            return new Type[0];
        }

        public void Create(Cqrs.Context context, IDependencyResolver resolver)
        {
            IStoreEvents eventStore = m_ConfigureEventStore(new CommitDispatcher(context.EventsPublisher)).Build();

            context.EventStore = new NEventStoreAdapter(eventStore,
                                                               resolver.HasService(typeof (IConstructAggregates))
                                                                   ? (IConstructAggregates)resolver.GetService(typeof(IConstructAggregates))
                                                                   : null);
        }

        public void Process(Cqrs.Context context, CqrsEngine cqrsEngine)
        {

        }
    }
}