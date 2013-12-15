using System;
using System.Linq;
using Inceptum.Cqrs.Configuration;
using Inceptum.Messaging.Contract;

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
/*
            string endpoint;
            if (!m_BoundedContext.EventRoutes.TryGetValue(@event.GetType(), out endpoint))
            {
                throw new InvalidOperationException(string.Format("bound context '{0}' does not support event '{1}'", m_BoundedContext.Name, @event.GetType()));
            }
            m_CqrsEngine.PublishEvent(@event, m_BoundedContext.Name, endpoint);
*/
            m_CqrsEngine.PublishEvent(@event, m_BoundedContext);
        }
 
    }
}