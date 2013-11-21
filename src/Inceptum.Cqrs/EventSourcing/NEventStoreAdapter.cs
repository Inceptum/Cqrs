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

        public NEventStoreAdapter(IStoreEvents storeEvents, IConstructAggregates aggregateFactory = null)
        {
            m_StoreEvents = storeEvents;
            Repository = new EventStoreRepository(storeEvents, aggregateFactory ?? new AggregateFactory(), new ConflictDetector());
        }

        public IRepository Repository { get; private set; }
        public IEnumerable<object> GetEventsFrom(DateTime @from, params Type[] types)
        {
            return m_StoreEvents.Advanced.GetFrom(@from).SelectMany(c => c.Events).Where(e => types.Length==0 || types.Contains(e.Body.GetType())).Select(message => message.Body);
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