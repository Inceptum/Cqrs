using System;
using Inceptum.Cqrs.Configuration;

namespace Inceptum.Cqrs
{
    class EventsPublisher : IEventPublisher
    {
        private readonly CqrsEngine m_CqrsEngine;
        private readonly BoundedContext m_BoundedContext;

        public EventsPublisher(CqrsEngine cqrsEngine,BoundedContext boundedContext)
        {
            m_BoundedContext = boundedContext;
            m_CqrsEngine = cqrsEngine;
        }

        public void PublishEvent(object @event)
        {
            if (@event == null) throw new ArgumentNullException("event");
            m_CqrsEngine.PublishEvent(@event, m_BoundedContext.Name);
        }
 
    }
}