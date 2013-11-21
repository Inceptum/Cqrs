using System;
using System.Collections.Generic;
using System.Linq;

namespace Inceptum.Cqrs.Configuration
{
    class SubscriptionDescriptor : IBoundedContextDescriptor
    {
        private readonly Dictionary<Type, string> m_EventsSubscriptions;
        private readonly List<CommandSubscription> m_CommandsSubscriptions;

        public SubscriptionDescriptor(Dictionary<Type, string> eventsSubscriptions, List<CommandSubscription> commandsSubscriptions)
        {
            m_CommandsSubscriptions = commandsSubscriptions;
            m_EventsSubscriptions = eventsSubscriptions;
        }
        public IEnumerable<Type> GetDependencies()
        {
            return new Type[0];
        }


        public void Create(BoundedContext boundedContext, IDependencyResolver resolver)
        {
            var eventSubscriptions = from pair in m_EventsSubscriptions
                                     group pair by pair.Value
                                         into grouping
                                         select new { endpoint = grouping.Key, types = grouping.Select(g => g.Key) };
            boundedContext.EventsSubscriptions = eventSubscriptions.ToDictionary(o => o.endpoint, o => o.types);
            boundedContext.CommandsSubscriptions = m_CommandsSubscriptions;
        }

        public void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine)
        {

        }
    }
}