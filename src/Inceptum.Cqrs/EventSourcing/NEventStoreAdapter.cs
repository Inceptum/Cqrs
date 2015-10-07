using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CommonDomain;
using CommonDomain.Core;
using CommonDomain.Persistence;
using CommonDomain.Persistence.EventStore;
using NEventStore;

namespace Inceptum.Cqrs.EventSourcing
{
    public class NEventStoreAdapter : IEventStoreAdapter
    {
        private readonly IStoreEvents m_StoreEvents;
        private readonly Action m_Bootstrap;
        private readonly IConstructAggregates m_AggregateFactory;

        public NEventStoreAdapter(IStoreEvents storeEvents, Action bootstrap = null, IConstructAggregates aggregateFactory = null)
        {
            if (storeEvents == null) throw new ArgumentNullException("storeEvents");
            m_StoreEvents = storeEvents;
            m_Bootstrap = bootstrap;
            m_AggregateFactory = aggregateFactory;
        }

        public void Bootstrap()
        {
            if(m_Bootstrap != null)
                m_Bootstrap();
        }

        public Func<IRepository> Repository {
            get
            {
                return () => new EventStoreRepository(m_StoreEvents, m_AggregateFactory ?? new AggregateFactory(), new ConflictDetector());
            }
        }
        public IEnumerable<object> GetEventsFrom(DateTime @from, Guid? aggregateId, params Type[] types)
        {
            var commits = aggregateId.HasValue
                ? m_StoreEvents.Advanced.GetFrom(aggregateId.Value, 0, int.MaxValue)
                : m_StoreEvents.Advanced.GetFrom(@from);

            return commits                
                .SelectMany(c => c.Events)
                .Where(e => types.Length==0 || types.Contains(e.Body.GetType())).Select(message => message.Body);
        }


          
        //TODO:  resolve aggregates from IoC
        public class AggregateFactory : IConstructAggregates
        {
            public IAggregate Build(Type type, Guid id, IMemento snapshot)
            {
                ConstructorInfo constructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(Guid), typeof(IMemento) }, null);
                return constructor.Invoke(new object[] {id, snapshot}) as IAggregate;
            }
        } 
    }
}