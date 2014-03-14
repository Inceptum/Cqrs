using System;
using System.Collections.Generic;

namespace Inceptum.Cqrs.Configuration
{
    internal class EventStoreDescriptor<T> : EventStoreDescriptor where T : IEventStoreAdapter
    {
        public override IEnumerable<Type> GetDependencies()
        {
            return new Type[] { typeof(T) };
        }

        public override void Create(BoundedContext boundedContext, IDependencyResolver resolver)
        {
            EventStoreAdapter = (T)resolver.GetService(typeof(T));
        }
    }

    class EventStoreDescriptor : IBoundedContextDescriptor
    {
        protected IEventStoreAdapter EventStoreAdapter { get; set; }

        protected EventStoreDescriptor()
        {
          
        }

        public EventStoreDescriptor(IEventStoreAdapter eventStoreAdapter)
        {
            EventStoreAdapter = eventStoreAdapter;
        }

        public virtual IEnumerable<Type> GetDependencies()
        {
            return new Type[0];
        }

        public virtual void Create(BoundedContext boundedContext, IDependencyResolver resolver)
        {
        }

        public void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine)
        {
            boundedContext.EventStore = EventStoreAdapter;
        }
    }
}