using System;
using Inceptum.Cqrs.Configuration;

namespace Inceptum.Cqrs
{
    public class EventsPublisher : IEventPublisher
    {
        private readonly CqrsEngine m_CqrsEngine;
        private readonly Context m_Context;

        public EventsPublisher(CqrsEngine cqrsEngine,Context context)
        {
            m_Context = context;
            m_CqrsEngine = cqrsEngine;
        }

        public void PublishEvent(object @event)
        {
            if (@event == null) throw new ArgumentNullException("event");
            m_CqrsEngine.PublishEvent(@event, m_Context.Name);
        }
 
    }
}